import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

type NotificationRow = {
  id: string;
  user_id: string;
  type: string;
  title: string | null;
  message: string | null;
  actor_id: string | null;
  entity_id: string | null;
  entity_type: string | null;
};

type DeviceTokenRow = {
  id: string;
  token: string;
  platform: string | null;
  device_name: string | null;
  created_at: string | null;
  last_seen_at: string | null;
};

type FcmFailure = {
  device_id: string;
  status: number;
  code: string | null;
  body: string;
};

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type, x-nightout-push-secret",
};

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const expectedSecret = Deno.env.get("NIGHTOUT_PUSH_WEBHOOK_SECRET");
    const receivedSecret = req.headers.get("x-nightout-push-secret");

    if (!expectedSecret || receivedSecret !== expectedSecret) {
      return json({ error: "Unauthorized" }, 401);
    }

    const { notification_id } = await req.json();

    if (!notification_id) {
      return json({ error: "notification_id is required" }, 400);
    }

    const supabaseUrl = Deno.env.get("SUPABASE_URL");
    const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");

    if (!supabaseUrl || !serviceRoleKey) {
      return json({ error: "Supabase service env is missing" }, 500);
    }

    const supabase = createClient(supabaseUrl, serviceRoleKey);

    const { data: notification, error: notificationError } = await supabase
      .from("notifications")
      .select("id,user_id,type,title,message,actor_id,entity_id,entity_type")
      .eq("id", notification_id)
      .single<NotificationRow>();

    if (notificationError || !notification) {
      return json({ error: "Notification not found", details: notificationError?.message }, 404);
    }

    const { data: devices, error: devicesError } = await supabase
      .from("device_tokens")
      .select("id,token,platform,device_name,created_at,last_seen_at")
      .eq("user_id", notification.user_id)
      .returns<DeviceTokenRow[]>();

    if (devicesError) {
      return json({ error: "Device tokens read failed", details: devicesError.message }, 500);
    }

    const validDevices = keepLatestDeviceOnly(
      (devices ?? []).filter((device) =>
        typeof device.token === "string" && device.token.trim().length > 0
      ),
    );

    if (validDevices.length === 0) {
      return json({ sent: 0, failed: 0, message: "No device token for user" });
    }

    const accessToken = await getFirebaseAccessToken();
    const projectId = requiredEnv("FIREBASE_PROJECT_ID");

    let sent = 0;
    let failed = 0;
    let removed = 0;
    const failures: FcmFailure[] = [];

    for (const device of validDevices) {
      const result = await sendFcm(projectId, accessToken, device.token, notification);

      if (result.ok) {
        sent++;
        continue;
      }

      failed++;
      failures.push({
        device_id: device.id,
        status: result.error.status,
        code: result.error.code,
        body: result.error.body,
      });

      if (shouldDeleteDeviceToken(result.error.body)) {
        const { error: deleteError } = await supabase
          .from("device_tokens")
          .delete()
          .eq("id", device.id);

        if (!deleteError) {
          removed++;
        } else {
          console.warn("[send-push] token cleanup failed", device.id, deleteError.message);
        }
      }
    }

    return json({
      success: failed === 0,
      sent,
      failed,
      removed,
      failures,
    });
  } catch (error) {
    console.error("[send-push] error", error);
    return json({ error: String(error) }, 500);
  }
});

function keepLatestDeviceOnly(devices: DeviceTokenRow[]): DeviceTokenRow[] {
  const latest = [...devices]
    .sort((a, b) => getDeviceTimestamp(b) - getDeviceTimestamp(a))
    .at(0);

  if (!latest) {
    return [];
  }

  return [{ ...latest, token: latest.token.trim() }];
}

function getDeviceTimestamp(device: DeviceTokenRow): number {
  const raw = device.last_seen_at || device.created_at || "";
  const time = Date.parse(raw);

  return Number.isFinite(time) ? time : 0;
}

async function sendFcm(
  projectId: string,
  accessToken: string,
  token: string,
  notification: NotificationRow,
): Promise<{ ok: true } | { ok: false; error: { status: number; code: string | null; body: string } }> {
  const title = notification.title || "NightOut";
  const body = notification.message || "Nouvelle notification";

  const data = {
    notification_id: notification.id,
    type: notification.type ?? "",
    actor_id: notification.actor_id ?? "",
    entity_id: notification.entity_id ?? "",
    entity_type: notification.entity_type ?? "",
    click_route: getClickRoute(notification),
    title,
    body,
  };

  const response = await fetch(
    `https://fcm.googleapis.com/v1/projects/${projectId}/messages:send`,
    {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        message: {
          token,
          notification: { title, body },
          data,
          android: {
            priority: "HIGH",
            ttl: "604800s",
            notification: {
              channel_id: "spotiz_default_channel",
              sound: "default",
              tag: notification.id,
            },
          },
          fcm_options: {
            analytics_label: notification.type || "notification",
          },
        },
      }),
    },
  );

  if (response.ok) {
    return { ok: true };
  }

  const responseBody = await response.text();

  return {
    ok: false,
    error: {
      status: response.status,
      code: extractFcmErrorCode(responseBody),
      body: responseBody,
    },
  };
}

function getClickRoute(notification: NotificationRow): string {
  const type = notification.type ?? "";
  const entityType = notification.entity_type ?? "";

  if (type === "private_message" || type === "direct_message") {
    return "ConversationPage";
  }

  if (entityType === "official_event" || type === "official_event_created") {
    return "OfficialEventDetailPage";
  }

  if (entityType === "friend_group" || entityType === "group") {
    return "GroupDetailPage";
  }

  if (entityType === "ephemeral_event" || type.startsWith("ephemeral_event_")) {
    return "EphemeralEventsPage";
  }

  if (type === "friend_request" || type === "friend_accepted") {
    return "FriendsPage";
  }

  return "NotificationsPage";
}

function extractFcmErrorCode(body: string): string | null {
  try {
    const parsed = JSON.parse(body);
    const details = parsed?.error?.details;

    if (Array.isArray(details)) {
      for (const detail of details) {
        if (typeof detail?.errorCode === "string") {
          return detail.errorCode;
        }
      }
    }

    if (typeof parsed?.error?.status === "string") {
      return parsed.error.status;
    }
  } catch {
    return null;
  }

  return null;
}

function shouldDeleteDeviceToken(body: string): boolean {
  const code = extractFcmErrorCode(body);

  return code === "UNREGISTERED" ||
    code === "INVALID_ARGUMENT" ||
    body.includes("registration-token-not-registered") ||
    body.includes("Requested entity was not found");
}

async function getFirebaseAccessToken(): Promise<string> {
  const clientEmail = requiredEnv("FIREBASE_CLIENT_EMAIL");
  const privateKey = normalizePrivateKey(requiredEnv("FIREBASE_PRIVATE_KEY"));

  const now = Math.floor(Date.now() / 1000);
  const header = { alg: "RS256", typ: "JWT" };
  const claim = {
    iss: clientEmail,
    scope: "https://www.googleapis.com/auth/firebase.messaging",
    aud: "https://oauth2.googleapis.com/token",
    iat: now,
    exp: now + 3600,
  };

  const unsignedJwt = `${base64UrlJson(header)}.${base64UrlJson(claim)}`;
  const signature = await signJwt(unsignedJwt, privateKey);
  const jwt = `${unsignedJwt}.${signature}`;

  const response = await fetch("https://oauth2.googleapis.com/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
      assertion: jwt,
    }),
  });

  if (!response.ok) {
    throw new Error(`Firebase OAuth failed: ${response.status} ${await response.text()}`);
  }

  const payload = await response.json();
  return payload.access_token;
}

function normalizePrivateKey(rawValue: string): string {
  let value = rawValue.trim();

  if (value.includes('"private_key"')) {
    try {
      const parsed = JSON.parse(value);
      if (typeof parsed.private_key === "string") {
        value = parsed.private_key;
      }
    } catch {
      // Continue avec la valeur brute : les erreurs seront remontees par Firebase/OAuth.
    }
  } else if (
    (value.startsWith('"') && value.endsWith('"')) ||
    (value.startsWith("'") && value.endsWith("'"))
  ) {
    value = value.slice(1, -1);
  }

  return value.replace(/\\n/g, "\n");
}

async function signJwt(unsignedJwt: string, privateKeyPem: string): Promise<string> {
  const pem = privateKeyPem
    .replace("-----BEGIN PRIVATE KEY-----", "")
    .replace("-----END PRIVATE KEY-----", "")
    .replace(/\s/g, "");

  const binary = Uint8Array.from(atob(pem), (char) => char.charCodeAt(0));

  const key = await crypto.subtle.importKey(
    "pkcs8",
    binary,
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["sign"],
  );

  const signature = await crypto.subtle.sign(
    "RSASSA-PKCS1-v1_5",
    key,
    new TextEncoder().encode(unsignedJwt),
  );

  return base64Url(new Uint8Array(signature));
}

function base64UrlJson(value: unknown): string {
  return base64Url(new TextEncoder().encode(JSON.stringify(value)));
}

function base64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary)
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/g, "");
}

function requiredEnv(name: string): string {
  const value = Deno.env.get(name);

  if (!value) {
    throw new Error(`${name} is missing`);
  }

  return value;
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...corsHeaders,
      "Content-Type": "application/json",
    },
  });
}
