(function () {
  const config = window.SpotizConfig;
  const sessionKey = "spotiz-web-session";

  function getSession() {
    try {
      return JSON.parse(localStorage.getItem(sessionKey) || "null");
    } catch {
      return null;
    }
  }

  function setSession(session) {
    if (!session) {
      localStorage.removeItem(sessionKey);
      return;
    }

    localStorage.setItem(sessionKey, JSON.stringify(session));
  }

  function authHeader() {
    const session = getSession();
    return session?.access_token
      ? `Bearer ${session.access_token}`
      : `Bearer ${config.supabaseAnonKey}`;
  }

  async function request(path, options = {}) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), options.timeoutMs || 8000);
    const headers = {
      apikey: config.supabaseAnonKey,
      Authorization: authHeader(),
      "Content-Type": "application/json",
      Prefer: options.prefer || "return=representation",
      ...options.headers
    };

    try {
      const response = await fetch(`${config.supabaseUrl}${path}`, {
        ...options,
        headers,
        signal: controller.signal
      });

      const text = await response.text();
      const data = text ? JSON.parse(text) : null;

      if (!response.ok) {
        const message = data?.msg || data?.message || data?.error_description || response.statusText;
        throw new Error(message);
      }

      return data;
    } catch (error) {
      if (error.name === "AbortError") {
        throw new Error("Supabase ne répond pas pour le moment.");
      }

      throw error;
    } finally {
      clearTimeout(timeout);
    }
  }

  function select(table, query) {
    return request(`/rest/v1/${table}?${query}`, {
      method: "GET",
      prefer: undefined
    });
  }

  function insert(table, payload) {
    return request(`/rest/v1/${table}`, {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  function patch(table, query, payload) {
    return request(`/rest/v1/${table}?${query}`, {
      method: "PATCH",
      body: JSON.stringify(payload)
    });
  }

  function rpc(name, payload = {}) {
    return request(`/rest/v1/rpc/${name}`, {
      method: "POST",
      body: JSON.stringify(payload)
    });
  }

  async function signIn(email, password) {
    const data = await request("/auth/v1/token?grant_type=password", {
      method: "POST",
      body: JSON.stringify({ email, password })
    });
    setSession(data);
    return data;
  }

  async function signUp(email, password, metadata = {}) {
    return request("/auth/v1/signup", {
      method: "POST",
      body: JSON.stringify({
        email,
        password,
        data: metadata
      })
    });
  }

  function signInWithGoogle(accountType = "user") {
    const redirectTo = `${window.location.origin}/?route=auth`;
    const params = new URLSearchParams({
      provider: "google",
      redirect_to: redirectTo
    });

    if (accountType && accountType !== "user") {
      params.set("scopes", "openid email profile");
    }

    sessionStorage.setItem("spotiz-google-account-type", accountType);
    window.location.href = `${config.supabaseUrl}/auth/v1/authorize?${params.toString()}`;
  }

  async function signOut() {
    try {
      await request("/auth/v1/logout", { method: "POST" });
    } finally {
      setSession(null);
    }
  }

  function resetPassword(email) {
    return request("/auth/v1/recover", {
      method: "POST",
      body: JSON.stringify({ email })
    });
  }

  function currentUserId() {
    const token = getSession()?.access_token;
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
      return payload.sub || null;
    } catch {
      return null;
    }
  }

  async function getProfile() {
    const id = currentUserId();
    if (!id) return null;
    const rows = await select("profiles", `select=*&id=eq.${encodeURIComponent(id)}&limit=1`);
    return rows[0] || null;
  }

  async function updateProfile(payload) {
    const id = currentUserId();
    if (!id) throw new Error("Connecte-toi pour modifier ton profil.");
    const rows = await patch("profiles", `id=eq.${encodeURIComponent(id)}`, payload);
    return rows[0] || null;
  }

  function getBars() {
    return select(
      "bars",
      "select=id,name,address,description,category,icon,latitude,longitude,total_present,logo_url,cover_url,is_active,status,is_verified,is_premium,slug&is_active=eq.true&status=eq.approved&order=total_present.desc&limit=30"
    );
  }

  function getEvents() {
    return select(
      "ephemeral_events",
      "select=id,title,description,place_name,address,category,visibility,start_at,expires_at,latitude,longitude,status,is_active,creator_id&is_active=eq.true&status=eq.published&order=start_at.asc&limit=30"
    );
  }

  function getFriends() {
    return rpc("get_friends", {});
  }

  function getConversations() {
    return rpc("get_conversations", {});
  }

  function getDirectMessages(partnerId) {
    return rpc("get_direct_messages", { p_partner_id: partnerId, p_limit: 50 });
  }

  function getNotifications(limit = 80) {
    return rpc("get_notifications", { p_limit: limit });
  }

  function markNotificationsRead() {
    return rpc("mark_notifications_read", {});
  }

  function sendMessage(receiverId, content) {
    return insert("direct_messages", {
      receiver_id: receiverId,
      content,
      type: "text"
    });
  }

  function checkIn(barId) {
    return rpc("check_in", {
      p_bar_id: barId,
      p_latitude: null,
      p_longitude: null
    });
  }

  function joinEvent(eventId) {
    return insert("ephemeral_event_participants", {
      event_id: eventId,
      user_id: currentUserId(),
      status: "joined"
    });
  }

  function createEvent(payload) {
    return insert("ephemeral_events", payload);
  }

  function createProEvent(payload) {
    return insert("official_events", payload);
  }

  window.SpotizApi = {
    getSession,
    setSession,
    signIn,
    signUp,
    signInWithGoogle,
    signOut,
    resetPassword,
    currentUserId,
    getProfile,
    updateProfile,
    getBars,
    getEvents,
    getFriends,
    getConversations,
    getDirectMessages,
    getNotifications,
    markNotificationsRead,
    sendMessage,
    checkIn,
    joinEvent,
    createEvent,
    createProEvent
  };
})();
