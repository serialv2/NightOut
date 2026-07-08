const api = window.SpotizApi;
const app = document.querySelector(".app-shell");
const views = [...document.querySelectorAll("[data-view]")];
const routeButtons = [...document.querySelectorAll("[data-route]")];
const pageTitle = document.querySelector("[data-page-title]");
const toast = document.querySelector("[data-toast]");

const state = {
  bars: [
    {
      id: "demo-la-maison",
      name: "La Maison",
      category: "Bar festif",
      address: "18 rue Massena, Lille",
      phone: "03 20 00 00 12",
      instagram: "@lamaison_lille",
      website: "lamaison-lille.fr",
      description: "Un spot festif du Vieux-Lille, parfait quand tu veux rejoindre une ambiance deja lancee.",
      icon: "&#128293;",
      total_present: 128,
      friends_present: 4,
      open: true,
      event: "DJ set des 22h",
      distance: "450 m",
      eta: "6 min",
      x: 54,
      y: 30
    },
    {
      id: "demo-joseph",
      name: "Joseph",
      category: "Cocktail bar",
      address: "9 place du Concert, Lille",
      phone: "03 20 00 00 28",
      instagram: "@joseph_lille",
      website: "joseph-lille.fr",
      description: "Cocktails, before et terrasse vivante. Le bon repere pour savoir si la soiree prend.",
      icon: "&#127864;",
      total_present: 86,
      friends_present: 2,
      open: true,
      event: "Before rooftop",
      distance: "700 m",
      eta: "9 min",
      x: 28,
      y: 54
    },
    {
      id: "demo-le-quai",
      name: "Le Quai",
      category: "Bar musical",
      address: "2 quai du Wault, Lille",
      phone: "03 20 00 00 44",
      instagram: "@lequai_lille",
      website: "lequai-lille.fr",
      description: "Un bar plus musical, plus pose en debut de soiree, qui monte quand le DJ set commence.",
      icon: "&#127911;",
      total_present: 42,
      friends_present: 0,
      open: true,
      event: "DJ set a 22h30",
      distance: "1,1 km",
      eta: "14 min",
      x: 72,
      y: 61
    }
  ],
  events: [
    { id: "demo-event-1", title: "Before rooftop au Joseph", meta: "Ce soir · 21 participants · Lille", type: "today friends", badge: "Ce soir" },
    { id: "demo-event-2", title: "Tournée vieux Lille", meta: "Demain · 12 participants · gratuit", type: "free", badge: "Gratuit" },
    { id: "demo-event-3", title: "DJ set au Quai", meta: "Vendredi · 46 participants · 3 amis", type: "friends", badge: "Avec amis" }
  ],
  friends: [
    { name: "Lola", place: "Au Joseph", initials: "LO" },
    { name: "Mathis", place: "Sur la route", initials: "MA" },
    { name: "Inès", place: "La Maison", initials: "IN" }
  ],
  groups: [
    { name: "Soirée Lille", meta: "6 membres · sortie prévue ce soir" },
    { name: "Afterwork", meta: "12 membres · actif le jeudi" },
    { name: "BDE", meta: "34 membres · événements fréquents" }
  ],
  conversations: [
    {
      name: "Lola",
      text: "On se retrouve au Joseph ?",
      initials: "LO",
      status: "Au Joseph · en ligne",
      time: "21:12",
      unread: 2,
      kind: "direct",
      messages: [
        { text: "Tu sors ce soir ?", me: false, time: "20:48" },
        { text: "Oui, je regarde les spots sur Spotiz.", me: true, time: "20:50" },
        { text: "Joseph a l'air bien rempli.", me: false, time: "21:12" }
      ]
    },
    {
      name: "Groupe Soirée Lille",
      text: "La Maison est chaude ce soir",
      initials: "SL",
      status: "6 membres · 4 actifs",
      time: "20:58",
      unread: 5,
      kind: "group",
      messages: [
        { text: "Lola : Joseph ou La Maison ?", me: false, time: "20:36" },
        { text: "Mathis : La Maison est blindée.", me: false, time: "20:44" },
        { text: "Steen : go Joseph 21h30.", me: true, time: "20:58" }
      ]
    },
    {
      name: "Mathis",
      text: "Je pars dans 10 minutes",
      initials: "MA",
      status: "Sur la route",
      time: "20:41",
      unread: 0,
      kind: "direct",
      messages: [
        { text: "Je pars dans 10 minutes", me: false, time: "20:41" },
        { text: "Je te garde une place.", me: true, time: "20:42" }
      ]
    }
  ],
  conversationFilter: "all",
  conversationSearch: "",
  selectedConversationIndex: 0,
  activities: [
    "Baptiste a publié une photo du bar.",
    "18 check-ins dans la dernière heure.",
    "Happy hour prolongée jusqu'à 22h."
  ],
  notifications: [
    { id: "notif-1", type: "direct_message", title: "Nouveau message privé", message: "Lola t'a envoyé un message.", created_at: new Date(Date.now() - 2 * 60 * 1000).toISOString(), entity_type: "direct_message", unread: true },
    { id: "notif-2", type: "ephemeral_event_friend", title: "Sortie bientôt", message: "Before rooftop commence bientôt.", created_at: new Date(Date.now() - 18 * 60 * 1000).toISOString(), entity_type: "ephemeral_event", unread: true },
    { id: "notif-3", type: "group_event", title: "Nouvelle sortie de groupe", message: "Soirée Lille propose Joseph à 21h30.", created_at: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), entity_type: "friend_group", unread: true },
    { id: "notif-4", type: "friend_request", title: "Nouvelle demande d'ami", message: "Inès souhaite t'ajouter en ami.", created_at: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), entity_type: "friendship", unread: false },
    { id: "notif-5", type: "approved", title: "Projet approuvé", message: "Ta fiche Joseph est validée côté pro.", created_at: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(), entity_type: "bar", unread: false }
  ],
  notificationFilter: "all",
  selectedBarId: "demo-la-maison",
  mapFilter: "all",
  selectedEventId: "demo-event-1",
  selectedFriendIndex: 0,
  selectedGroupIndex: 0,
  selectedConversation: null,
  profile: null,
  online: false
};

const titles = {
  map: "Carte Spotiz",
  events: "Sorties éphémères",
  "event-detail": "Détail sortie",
  friends: "Amis et groupes",
  "friend-profile": "Profil ami",
  "group-detail": "Groupe",
  messages: "Messages",
  notifications: "Notifications",
  profile: "Profil",
  settings: "Réglages",
  pro: "Espace pro",
  "pro-stats": "Statistiques pro",
  rewards: "Récompenses",
  "register-bar": "Fiche bar pro",
  auth: "Connexion",
  register: "Inscription",
  "forgot-password": "Mot de passe oublié",
  "create-event": "Créer une sortie",
  confirmed: "Email confirmé",
  bar: "Fiche bar",
  creator: "Profil organisateur"
};

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function initials(name) {
  const parts = String(name || "?").trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return "?";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

function formatDate(value) {
  if (!value) return "Date à confirmer";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Date à confirmer";

  return date.toLocaleDateString("fr-FR", {
    weekday: "short",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function badgeForEvent(event) {
  const start = event.start_at ? new Date(event.start_at) : null;
  if (!start || Number.isNaN(start.getTime())) return event.badge || "Sortie";

  const now = new Date();
  const sameDay = start.toDateString() === now.toDateString();
  const tomorrow = new Date(now);
  tomorrow.setDate(now.getDate() + 1);

  if (sameDay) return "Ce soir";
  if (start.toDateString() === tomorrow.toDateString()) return "Demain";
  return formatDate(event.start_at);
}

function notificationTitle(notification) {
  if (notification.title) return notification.title;
  return {
    approved: "Projet approuvé",
    rejected: "Projet refusé",
    friend_request: "Nouvelle demande d'ami",
    friend_accepted: "Demande acceptée",
    private_message: "Nouveau message privé",
    direct_message: "Nouveau message privé",
    friend_invite_used: "Invitation utilisée",
    group_message: "Nouveau message de groupe",
    group_photo: "Nouvelle photo de groupe",
    group_video: "Nouvelle vidéo de groupe",
    group_event: "Nouvelle sortie de groupe",
    group_event_response: "Réponse à une sortie",
    ephemeral_event_friend: "Sortie d'un ami",
    ephemeral_event_group: "Sortie de groupe",
    ephemeral_event_cancelled: "Sortie annulée",
    invite_reward: "Crédits gagnés",
    moderation_report_dismissed: "Signalement examiné",
    moderation_report_action_taken: "Signalement traité",
    moderation_warning: "Avertissement modération",
    moderation_ban: "Compte banni"
  }[notification.type] || "Notification";
}

function notificationMessage(notification) {
  if (notification.message) return notification.message;
  return {
    friend_request: "Quelqu'un souhaite vous ajouter en ami.",
    friend_accepted: "Votre demande d'ami a été acceptée.",
    private_message: "Vous avez reçu un nouveau message.",
    direct_message: "Vous avez reçu un nouveau message.",
    friend_invite_used: "Un utilisateur a rejoint Spotiz avec votre invitation.",
    group_message: "Nouveau message dans un groupe.",
    group_photo: "Nouvelle photo dans un groupe.",
    group_video: "Nouvelle vidéo dans un groupe.",
    group_event: "Une sortie vient d'être proposée.",
    ephemeral_event_friend: "Un ami a créé une sortie près de vous.",
    ephemeral_event_group: "Une sortie de groupe vient d'être proposée.",
    ephemeral_event_cancelled: "Une sortie à laquelle vous étiez invité a été annulée.",
    moderation_report_dismissed: "Votre signalement a été examiné.",
    moderation_report_action_taken: "Votre signalement a été traité.",
    moderation_warning: "Vous avez reçu un avertissement de modération.",
    moderation_ban: "Votre compte a été banni."
  }[notification.type] || notification.type || "";
}

function notificationIcon(notification) {
  return {
    approved: "✅",
    rejected: "❌",
    friend_request: "👋",
    friend_accepted: "🤝",
    private_message: "💬",
    direct_message: "💬",
    friend_invite_used: "🎉",
    group_message: "💬",
    group_photo: "📷",
    group_video: "🎥",
    group_event: "🍻",
    group_event_response: "✅",
    ephemeral_event_friend: "🎉",
    ephemeral_event_group: "🍻",
    ephemeral_event_cancelled: "❌",
    invite_reward: "🪙",
    moderation_report_dismissed: "✅",
    moderation_report_action_taken: "⚠",
    moderation_warning: "⚠",
    moderation_ban: "⛔"
  }[notification.type] || "🔔";
}

function notificationTimeAgo(notification) {
  const value = notification.created_at || notification.createdAt;
  if (!value) return "";
  const created = new Date(value);
  if (Number.isNaN(created.getTime())) return "";
  const diff = Date.now() - created.getTime();
  const minute = 60 * 1000;
  const hour = 60 * minute;
  const day = 24 * hour;
  if (diff < minute) return "à l'instant";
  if (diff < hour) return `il y a ${Math.floor(diff / minute)} min`;
  if (diff < day) return `il y a ${Math.floor(diff / hour)}h`;
  return `il y a ${Math.floor(diff / day)}j`;
}

function isNotificationUnread(notification) {
  return Boolean(notification.unread ?? !(notification.is_read || notification.isReadRaw || notification.read_at));
}

function notificationCategory(notification) {
  const type = String(notification.type || "").toLowerCase();
  const entityType = String(notification.entity_type || notification.entityType || "").toLowerCase();
  const text = `${notificationTitle(notification)} ${notificationMessage(notification)}`.toLowerCase();
  if (type.includes("message") || type === "direct_message" || type === "private_message") return "messages";
  if (type.includes("group") || entityType.includes("group")) return "groups";
  if (type.includes("event") || type.includes("ephemeral") || entityType.includes("event") || text.includes("sortie")) return "events";
  if (type.includes("friend") || entityType.includes("friend")) return "friends";
  return "other";
}

function routeForNotification(notification) {
  const type = String(notification.type || "").toLowerCase();
  const entityType = String(notification.entity_type || notification.entityType || "").toLowerCase();
  const text = `${notificationTitle(notification)} ${notificationMessage(notification)}`.toLowerCase();
  if (type === "direct_message" || type === "private_message") return "messages";
  if (type.includes("group") || entityType === "friend_group") return "group-detail";
  if (type.includes("event") || type.includes("ephemeral") || entityType.includes("event") || text.includes("sortie")) return "event-detail";
  if (type.includes("friend") || entityType === "friendship") return "friends";
  if (entityType === "bar" || type === "approved" || type === "rejected") return "pro";
  return "notifications";
}

function mapNotification(row) {
  return {
    ...row,
    unread: !(row.is_read || row.isReadRaw || row.read_at)
  };
}

function requireSession() {
  if (api?.currentUserId()) return true;
  showToast("Connecte-toi pour utiliser cette fonction.");
  setRoute("auth");
  return false;
}

function showToast(message) {
  toast.textContent = message;
  toast.classList.add("is-visible");
  clearTimeout(showToast.timeout);
  showToast.timeout = setTimeout(() => toast.classList.remove("is-visible"), 2800);
}

function setRoute(route, push = true) {
  const safeRoute = titles[route] ? route : "map";
  const publicRoutes = ["auth", "register", "forgot-password", "confirmed"];

  views.forEach((view) => view.classList.toggle("is-active", view.dataset.view === safeRoute));
  routeButtons.forEach((button) => {
    button.classList.toggle("is-active", button.dataset.route === safeRoute);
  });

  app.dataset.route = safeRoute;
  app.classList.toggle("public-mode", publicRoutes.includes(safeRoute));
  pageTitle.textContent = titles[safeRoute];

  if (push) {
    const path = safeRoute === "map" ? "/" : `/${safeRoute}`;
    history.pushState({ route: safeRoute }, "", path);
  }
}

function routeFromLocation() {
  const queryRoute = new URLSearchParams(window.location.search).get("route");
  if (queryRoute) return queryRoute;

  const path = window.location.pathname.replace(/^\/+|\/+$/g, "");
  if (path === "auth/confirmed") return "confirmed";
  return path || "map";
}

function restoreOAuthSessionFromHash() {
  if (!location.hash.includes("access_token")) return false;

  const hash = new URLSearchParams(location.hash.slice(1));
  const accessToken = hash.get("access_token");
  const refreshToken = hash.get("refresh_token");

  if (!accessToken || !refreshToken) return false;

  api?.setSession({
    access_token: accessToken,
    refresh_token: refreshToken,
    token_type: hash.get("token_type") || "bearer",
    expires_in: Number(hash.get("expires_in") || 3600)
  });

  history.replaceState({}, "", "/");
  showToast("Connexion Google réussie.");
  return true;
}

function mapBar(row, index = 0) {
  const fallbackPositions = [
    { x: 54, y: 30 },
    { x: 28, y: 54 },
    { x: 72, y: 61 },
    { x: 42, y: 70 },
    { x: 66, y: 42 }
  ];
  const fallback = fallbackPositions[index % fallbackPositions.length];
  const category = row.category || row.primary_category?.name || "Bar";
  const count = Number(row.total_present || row.totalPresent || 0);
  const friends = Number(row.friends_present || row.friendsPresent || 0);
  const level = count >= 80 ? "hot" : count >= 30 ? "good" : "calm";
  return {
    ...row,
    category,
    total_present: count,
    friends_present: friends,
    open: Boolean(row.open ?? row.is_open ?? true),
    address: row.address || "Adresse a confirmer",
    phone: row.phone || "Non renseigne",
    instagram: row.instagram || row.instagram_url || "",
    website: row.website || row.website_url || "",
    description: row.description || "Les donnees du lieu s'afficheront ici quand la fiche sera complete.",
    distance: row.distance || "A proximite",
    eta: row.eta || "",
    x: Number(row.x ?? row.map_x ?? fallback.x),
    y: Number(row.y ?? row.map_y ?? fallback.y),
    mood: count >= 80 ? "Tres anime" : count >= 30 ? "Anime" : "Calme",
    meta: `${count} present${count > 1 ? "s" : ""} - ${category}`,
    icon: row.icon || (count >= 80 ? "&#128293;" : count >= 30 ? "&#127864;" : "&#127911;"),
    level,
    gauge: Math.min(100, Math.max(8, Math.round((count / 140) * 100)))
  };
}

function visibleMapBars() {
  return state.bars.filter((bar) => {
    if (state.mapFilter === "open") return bar.open;
    if (state.mapFilter === "hot") return bar.level === "hot";
    if (state.mapFilter === "friends") return Number(bar.friends_present || 0) > 0;
    return true;
  });
}

function mapEvent(row) {
  return {
    ...row,
    meta: `${formatDate(row.start_at)} · ${row.place_name || row.address || "Lieu à confirmer"}`,
    type: `${row.start_at ? "today " : ""}${row.visibility || "public"}`,
    badge: badgeForEvent(row)
  };
}

function renderSpots() {
  const selected = state.bars.find((bar) => bar.id === state.selectedBarId) || state.bars[0];
  const selectedCard = document.querySelector("[data-selected-spot]");
  const visibleBars = visibleMapBars();
  const markerTarget = document.querySelector("[data-map-markers]");
  const listTarget = document.querySelector("[data-spot-list]");
  const totalTarget = document.querySelector("[data-map-total]");
  const hotTarget = document.querySelector("[data-map-hot-count]");
  const summaryTarget = document.querySelector("[data-map-summary]");
  const totalPresent = state.bars.reduce((sum, bar) => sum + Number(bar.total_present || 0), 0);
  const hotCount = state.bars.filter((bar) => bar.level === "hot").length;

  if (totalTarget) totalTarget.textContent = totalPresent;
  if (hotTarget) hotTarget.textContent = hotCount;
  if (summaryTarget) {
    summaryTarget.textContent = `${visibleBars.length} lieu${visibleBars.length > 1 ? "x" : ""} affiches`;
  }

  if (markerTarget) {
    markerTarget.innerHTML = visibleBars.map((spot) => `
      <button class="map-marker ${escapeHtml(spot.level)} ${spot.id === state.selectedBarId ? "is-selected" : ""}" type="button" data-action="select-bar" data-bar-id="${escapeHtml(spot.id)}" style="--x:${Number(spot.x)}%; --y:${Number(spot.y)}%;" aria-label="${escapeHtml(spot.name)}">
        <span>${spot.icon}</span>
        <strong>${escapeHtml(spot.total_present)}</strong>
      </button>
    `).join("");
  }

  if (selectedCard && selected) {
    selectedCard.innerHTML = `
      <div class="map-sheet-header">
        <div class="map-place-icon">${selected.icon}</div>
        <div class="map-sheet-title">
          <span>${escapeHtml(selected.category)}</span>
          <h2>${escapeHtml(selected.name)}</h2>
          <p>${escapeHtml(selected.address)}</p>
        </div>
        <span class="pill ${escapeHtml(selected.level)}">${escapeHtml(selected.mood)}</span>
      </div>
      <div class="map-pill-row">
        <span>${selected.open ? "Ouvert" : "Ferme"}</span>
        <span>${escapeHtml(selected.event || "Aucun evenement annonce")}</span>
        <span>${escapeHtml(selected.distance)}${selected.eta ? ` - ${escapeHtml(selected.eta)}` : ""}</span>
      </div>
      <div class="map-metric">
        <div>
          <strong>${escapeHtml(selected.total_present)}</strong>
          <span>presents maintenant</span>
        </div>
        <div>
          <strong>${escapeHtml(selected.friends_present)}</strong>
          <span>amis sur place</span>
        </div>
      </div>
      <div class="map-gauge" aria-label="Niveau d'ambiance">
        <span style="width:${Number(selected.gauge || 0)}%"></span>
      </div>
      <div class="map-quick-actions">
        <button class="primary-button" type="button" data-action="checkin" data-bar-id="${escapeHtml(selected.id)}">Check-in</button>
        <button class="ghost-button" type="button" data-action="directions">Itineraire</button>
        <button class="ghost-button" type="button" data-route="bar">Fiche</button>
      </div>
      <div class="map-info-grid">
        <span>${escapeHtml(selected.phone)}</span>
        <span>${escapeHtml(selected.instagram || "Instagram non renseigne")}</span>
        <span>${escapeHtml(selected.website || "Site non renseigne")}</span>
      </div>
      <p class="map-description">${escapeHtml(selected.description)}</p>
    `;
  }

  if (!listTarget) return;

  listTarget.innerHTML = visibleBars.map((spot) => `
    <button class="spot-card ${spot.id === state.selectedBarId ? "is-selected" : ""}" type="button" data-action="select-bar" data-bar-id="${escapeHtml(spot.id)}">
      <div class="spot-icon">${spot.icon}</div>
      <div>
        <h3>${escapeHtml(spot.name)}</h3>
        <p>${escapeHtml(spot.meta)}${spot.friends_present ? ` - ${escapeHtml(spot.friends_present)} amis` : ""}</p>
      </div>
      <span class="pill ${escapeHtml(spot.level)}">${escapeHtml(spot.mood)}</span>
    </button>
  `).join("");
}

function renderEvents(filter = "all") {
  const visible = filter === "all"
    ? state.events
    : state.events.filter((event) => String(event.type || "").includes(filter));

  document.querySelector("[data-events-list]").innerHTML = visible.map((eventItem) => `
    <article class="event-card">
      <span class="pill hot">${escapeHtml(eventItem.badge)}</span>
      <h3>${escapeHtml(eventItem.title)}</h3>
      <p>${escapeHtml(eventItem.meta)}</p>
      <div class="row-actions">
        <button class="primary-button" type="button" data-action="join-event" data-event-id="${escapeHtml(eventItem.id)}">J'y vais</button>
        <button class="ghost-button" type="button" data-action="open-event" data-event-id="${escapeHtml(eventItem.id)}">Voir</button>
        <button class="ghost-button" type="button" data-route="creator">Profil</button>
      </div>
    </article>
  `).join("");
}

function renderEventDetail() {
  const eventItem = state.events.find((item) => item.id === state.selectedEventId) || state.events[0];
  const target = document.querySelector("[data-event-detail]");
  if (!target || !eventItem) return;

  target.innerHTML = `
    <section class="panel">
      <span class="pill hot">${escapeHtml(eventItem.badge || "Sortie")}</span>
      <h2>${escapeHtml(eventItem.title)}</h2>
      <p>${escapeHtml(eventItem.meta || "Lieu à confirmer")}</p>
      <div class="avatar-row"><span>BA</span><span>LO</span><span>MA</span><span>+18</span></div>
      <div class="row-actions">
        <button class="primary-button" type="button" data-action="join-event" data-event-id="${escapeHtml(eventItem.id)}">J'y vais</button>
        <button class="ghost-button" type="button" data-route="creator">Profil organisateur</button>
        <button class="ghost-button" type="button" data-route="events">Retour</button>
      </div>
    </section>
    <section class="panel">
      <h2>Activité</h2>
      <div class="activity-feed">
        <article class="activity-item"><p>Lola a rejoint cette sortie.</p></article>
        <article class="activity-item"><p>Mathis a partagé le lieu au groupe.</p></article>
        <article class="activity-item"><p>Départ conseillé vers 21h30.</p></article>
      </div>
    </section>
  `;
}

function renderFriends() {
  document.querySelector("[data-friends-list]").innerHTML = state.friends.map((friend) => `
    <button class="friend-card" type="button" data-action="open-friend" data-friend-index="${state.friends.indexOf(friend)}">
      <div class="avatar">${escapeHtml(friend.initials || initials(friend.name))}</div>
      <div>
        <h3>${escapeHtml(friend.name)}</h3>
        <p>${escapeHtml(friend.place)}</p>
      </div>
    </button>
  `).join("");

  document.querySelector("[data-groups-list]").innerHTML = state.groups.map((group) => `
    <button class="group-card" type="button" data-action="open-group" data-group-index="${state.groups.indexOf(group)}">
      <h3>${escapeHtml(group.name)}</h3>
      <p>${escapeHtml(group.meta)}</p>
    </button>
  `).join("");
}

function renderFriendDetail() {
  const friend = state.friends[state.selectedFriendIndex] || state.friends[0];
  const target = document.querySelector("[data-friend-detail]");
  if (!target || !friend) return;

  target.innerHTML = `
    <section class="profile-card">
      <div class="profile-avatar">${escapeHtml(friend.initials || initials(friend.name))}</div>
      <h2>${escapeHtml(friend.name)}</h2>
      <p>${escapeHtml(friend.place)} · ouverte aux sorties</p>
      <div class="stats-row"><span><strong>31</strong> sorties</span><span><strong>12</strong> amis communs</span><span><strong>3</strong> badges</span></div>
      <button class="primary-button" type="button" data-route="messages">Envoyer un message</button>
      <button class="ghost-button" type="button" data-route="friends">Retour amis</button>
    </section>
    <section class="panel">
      <h2>Historique récent</h2>
      <div class="activity-feed">
        <article class="activity-item"><p>A rejoint Before rooftop au Joseph.</p></article>
        <article class="activity-item"><p>Présente sur la carte ce soir.</p></article>
        <article class="activity-item"><p>Fait partie du groupe Soirée Lille.</p></article>
      </div>
    </section>
  `;
}

function renderGroupDetail() {
  const group = state.groups[state.selectedGroupIndex] || state.groups[0];
  const target = document.querySelector("[data-group-detail]");
  if (!target || !group) return;

  target.innerHTML = `
    <section class="panel">
      <p class="kicker">Groupe</p>
      <h2>${escapeHtml(group.name)}</h2>
      <p>${escapeHtml(group.meta)}</p>
      <div class="avatar-row"><span>ST</span><span>LO</span><span>MA</span><span>IN</span></div>
      <div class="row-actions">
        <button class="primary-button" type="button" data-route="create-event">Créer une sortie</button>
        <button class="ghost-button" type="button" data-action="invite-friend">Inviter</button>
        <button class="ghost-button" type="button" data-route="friends">Retour</button>
      </div>
    </section>
    <section class="panel">
      <h2>Discussion</h2>
      <div class="activity-feed">
        <article class="activity-item"><p>Lola : Joseph ou La Maison ?</p></article>
        <article class="activity-item"><p>Mathis : La Maison est blindée.</p></article>
        <article class="activity-item"><p>Steen : go Joseph 21h30.</p></article>
      </div>
    </section>
  `;
}

function renderMessages() {
  const query = state.conversationSearch.trim().toLowerCase();
  const visible = state.conversations
    .map((conversation, index) => ({ ...conversation, index }))
    .filter((conversation) => {
      if (state.conversationFilter === "unread" && !conversation.unread) return false;
      if (state.conversationFilter === "groups" && conversation.kind !== "group") return false;
      if (!query) return true;
      return `${conversation.name} ${conversation.text}`.toLowerCase().includes(query);
    });

  document.querySelector("[data-conversations]").innerHTML = visible.map((conversation) => `
    <button class="conversation ${conversation.index === state.selectedConversationIndex ? "is-active" : ""}" type="button" data-action="open-conversation" data-conversation-index="${conversation.index}" data-partner-id="${escapeHtml(conversation.partner_id || "")}">
      <div class="avatar">${escapeHtml(conversation.initials || initials(conversation.name))}</div>
      <div class="conversation-copy">
        <h3>${escapeHtml(conversation.name)}</h3>
        <p>${escapeHtml(conversation.text)}</p>
      </div>
      <div class="conversation-meta">
        <span>${escapeHtml(conversation.time || "")}</span>
        ${conversation.unread ? `<strong>${escapeHtml(conversation.unread)}</strong>` : ""}
      </div>
    </button>
  `).join("") || `<div class="empty-state">Aucune discussion trouvee.</div>`;

  renderChat();
}

function renderChat() {
  const conversation = state.conversations[state.selectedConversationIndex] || state.conversations[0];
  const chatHead = document.querySelector("[data-chat-head]");
  const chatStream = document.querySelector("[data-chat-stream]");

  if (!conversation) {
    chatHead.innerHTML = `<div><h2>Aucune discussion</h2><p>Choisis un contact pour commencer.</p></div>`;
    chatStream.innerHTML = "";
    return;
  }

  chatHead.innerHTML = `
    <div class="avatar">${escapeHtml(conversation.initials || initials(conversation.name))}</div>
    <div class="chat-title"><h2>${escapeHtml(conversation.name)}</h2><p>${escapeHtml(conversation.status || "Conversation Spotiz")}</p></div>
    <div class="chat-actions">
      <button class="icon-button" type="button" data-action="share-location" aria-label="Partager ma position">&#128205;</button>
      <button class="icon-button" type="button" data-action="mute-conversation" aria-label="Mettre en sourdine">&#128276;</button>
      <button class="icon-button" type="button" data-action="report-conversation" aria-label="Signaler">&#9888;</button>
    </div>
  `;

  chatStream.innerHTML = (conversation.messages || []).map((message) => `
    <div class="bubble ${message.me ? "me" : ""}">
      <span>${escapeHtml(message.text)}</span>
      <small>${escapeHtml(message.time || "")}</small>
    </div>
  `).join("");
  chatStream.scrollTop = chatStream.scrollHeight;
}

function renderNotifications() {
  const unreadCount = state.notifications.filter(isNotificationUnread).length;
  const visible = state.notifications.filter((notification) => {
    if (state.notificationFilter === "all") return true;
    if (state.notificationFilter === "unread") return isNotificationUnread(notification);
    return notificationCategory(notification) === state.notificationFilter;
  });

  const unreadTarget = document.querySelector("[data-notifications-unread]");
  const markAllButton = document.querySelector("[data-mark-all-read]");
  const titleTarget = document.querySelector("[data-notifications-title]");
  if (unreadTarget) {
    unreadTarget.textContent = unreadCount > 0 ? `${unreadCount} non lue(s)` : "";
    unreadTarget.hidden = unreadCount === 0;
  }
  if (markAllButton) markAllButton.hidden = unreadCount === 0;
  if (titleTarget) {
    titleTarget.textContent = {
      all: "Centre de notifications",
      unread: "Notifications non lues",
      messages: "Notifications messages",
      groups: "Notifications groupes",
      events: "Notifications sorties"
    }[state.notificationFilter] || "Centre de notifications";
  }

  document.querySelector("[data-notifications-list]").innerHTML = visible.map((notification) => `
    <article class="activity-item notification-item ${isNotificationUnread(notification) ? "is-unread" : ""}" data-action="open-notification" data-notification-id="${escapeHtml(notification.id)}" tabindex="0" role="button">
      <div class="notification-main">
        <div class="notification-icon">${escapeHtml(notificationIcon(notification))}</div>
        <div>
          <h3>${escapeHtml(notificationTitle(notification))}</h3>
          <p>${escapeHtml(notificationMessage(notification))}</p>
          <small>${escapeHtml(notificationTimeAgo(notification))}</small>
        </div>
        <span class="notification-dot" aria-hidden="true"></span>
      </div>
      <div class="row-actions">
        <button class="ghost-button compact" type="button" data-action="open-notification" data-notification-id="${escapeHtml(notification.id)}">Ouvrir</button>
        ${isNotificationUnread(notification) ? `<button class="text-button compact" type="button" data-action="read-notification" data-notification-id="${escapeHtml(notification.id)}">Lu</button>` : ""}
      </div>
    </article>
  `).join("") || `
    <div class="notification-empty">
      <div>🔔</div>
      <h3>Aucune notification</h3>
      <p>Les demandes, groupes, sorties et invitations apparaitront ici.</p>
    </div>
  `;
}

function renderProfile() {
  if (!state.profile) return;

  const card = document.querySelector(".profile-card");
  const name = state.profile.display_name || state.profile.username || "Utilisateur";
  card.querySelector(".profile-avatar").textContent = initials(name);
  card.querySelector("h2").textContent = name;
  card.querySelector("p").textContent = `@${state.profile.username || "spotiz"} · ${state.profile.nights_out || 0} sorties`;
}

function renderPro() {
  document.querySelector("[data-pro-events]").innerHTML = [
    "Afterwork terrasse · jeudi 19h",
    "DJ set officiel · vendredi 22h",
    "Brunch lendemain de soirée · dimanche 11h"
  ].map((eventItem) => `
    <article class="pro-event">
      <h3>${escapeHtml(eventItem)}</h3>
      <p>Visible sur la carte et dans les sorties.</p>
    </article>
  `).join("");
}

function renderActivity() {
  document.querySelector("[data-activity-feed]").innerHTML = state.activities.map((item) => `
    <article class="activity-item"><p>${escapeHtml(item)}</p></article>
  `).join("");
}

function renderAll() {
  renderSpots();
  renderEvents();
  renderEventDetail();
  renderFriends();
  renderFriendDetail();
  renderGroupDetail();
  renderMessages();
  renderNotifications();
  renderProfile();
  renderPro();
  renderActivity();
}

async function loadSupabaseData() {
  if (!api) return;

  try {
    const [bars, events] = await Promise.all([
      api.getBars(),
      api.getEvents()
    ]);

    if (Array.isArray(bars) && bars.length) {
      state.bars = bars.map(mapBar);
      state.selectedBarId = state.bars[0].id;
    }

    if (Array.isArray(events) && events.length) {
      state.events = events.map(mapEvent);
    }

    state.online = true;
    renderSpots();
    renderEvents();
    renderEventDetail();
    showToast("Données Spotiz chargées.");
  } catch (error) {
    console.warn("[Spotiz] Données Supabase indisponibles :", error.message);
    showToast("Mode aperçu : certaines données Supabase sont privées.");
  }

  if (api.currentUserId()) {
    await loadPrivateData();
  }
}

async function loadPrivateData() {
  if (!api?.currentUserId()) return;

  try {
    state.profile = await api.getProfile();
    renderProfile();
  } catch (error) {
    console.warn("[Spotiz] Profil indisponible :", error.message);
  }

  try {
    const conversations = await api.getConversations();
    if (Array.isArray(conversations) && conversations.length) {
      state.conversations = conversations.map((item) => ({
        partner_id: item.partner_id,
        name: item.partner_display_name || item.partner_username || "Conversation",
        text: item.last_message || "Nouvelle conversation",
        initials: initials(item.partner_display_name || item.partner_username),
        status: "Conversation privee",
        time: "Maintenant",
        unread: item.unread_count || 0,
        kind: "direct",
        messages: [
          { text: item.last_message || "Nouvelle conversation", me: false, time: "Maintenant" }
        ]
      }));
      state.selectedConversationIndex = 0;
      renderMessages();
    }
  } catch (error) {
    console.warn("[Spotiz] Conversations indisponibles :", error.message);
  }

  await loadNotifications();
}

async function loadNotifications(showMessage = false) {
  if (!api?.currentUserId()) {
    renderNotifications();
    if (showMessage) showToast("Mode aperçu : connecte-toi pour synchroniser les notifications.");
    return;
  }

  try {
    const notifications = await api.getNotifications(80);
    if (Array.isArray(notifications)) {
      state.notifications = notifications.map(mapNotification);
      renderNotifications();
      if (showMessage) showToast("Notifications actualisées.");
    }
  } catch (error) {
    console.warn("[Spotiz] Notifications indisponibles :", error.message);
    renderNotifications();
    if (showMessage) showToast("Mode aperçu : notifications Supabase indisponibles.");
  }
}

async function handleAction(button) {
  const action = button.dataset.action;

  if (action === "toggle-theme") {
    const nextTheme = app.dataset.theme === "light" ? "dark" : "light";
    app.dataset.theme = nextTheme;
    localStorage.setItem("spotiz-theme", nextTheme);
    button.textContent = nextTheme === "light" ? "☾" : "☀";
    showToast(nextTheme === "light" ? "Thème clair activé." : "Thème sombre activé.");
    return;
  }

  if (action === "select-bar") {
    state.selectedBarId = button.dataset.barId;
    renderSpots();
    return;
  }

  if (action === "map-filter") {
    state.mapFilter = button.dataset.mapFilter || "all";
    button.parentElement.querySelectorAll(".filter").forEach((filter) => {
      filter.classList.toggle("is-active", filter === button);
    });
    renderSpots();
    showToast(`Filtre "${button.textContent.trim()}" applique.`);
    return;
  }

  if (action === "open-event") {
    state.selectedEventId = button.dataset.eventId;
    renderEventDetail();
    setRoute("event-detail");
    return;
  }

  if (action === "open-friend") {
    state.selectedFriendIndex = Number(button.dataset.friendIndex || 0);
    renderFriendDetail();
    setRoute("friend-profile");
    return;
  }

  if (action === "open-group") {
    state.selectedGroupIndex = Number(button.dataset.groupIndex || 0);
    renderGroupDetail();
    setRoute("group-detail");
    return;
  }

  if (action === "open-conversation") {
    state.selectedConversationIndex = Number(button.dataset.conversationIndex || 0);
    const conversation = state.conversations[state.selectedConversationIndex];
    if (conversation) {
      conversation.unread = 0;
      state.selectedConversation = conversation.partner_id || null;
    }
    renderMessages();
    return;
  }

  if (action === "conversation-filter") {
    state.conversationFilter = button.dataset.conversationFilter || "all";
    button.parentElement.querySelectorAll(".filter").forEach((filter) => {
      filter.classList.toggle("is-active", filter === button);
    });
    renderMessages();
    return;
  }

  if (action === "quick-reply") {
    const input = document.querySelector("[data-message-form] input[name='message']");
    input.value = button.dataset.message || "";
    input.focus();
    return;
  }

  if (action === "notification-filter") {
    state.notificationFilter = button.dataset.notificationFilter || "all";
    button.parentElement.querySelectorAll(".filter").forEach((filter) => {
      filter.classList.toggle("is-active", filter === button);
    });
    renderNotifications();
    return;
  }

  if (action === "refresh-notifications") {
    const originalText = button.textContent;
    button.disabled = true;
    button.textContent = "Actualisation...";
    try {
      await loadNotifications(true);
    } finally {
      button.disabled = false;
      button.textContent = originalText;
    }
    return;
  }

  if (action === "back-from-notifications") {
    setRoute("map");
    return;
  }

  if (action === "read-notification") {
    const notification = state.notifications.find((item) => item.id === button.dataset.notificationId);
    if (notification) {
      notification.unread = false;
      notification.is_read = true;
      notification.read_at = new Date().toISOString();
    }
    renderNotifications();
    return;
  }

  if (action === "open-notification") {
    const notification = state.notifications.find((item) => item.id === button.dataset.notificationId);
    if (notification) {
      notification.unread = false;
      notification.is_read = true;
      notification.read_at = new Date().toISOString();
      renderNotifications();
      const nextRoute = routeForNotification(notification);
      setRoute(nextRoute);
      showToast(nextRoute === "notifications" ? "Notification ouverte." : "Ouverture de la page liée.");
    }
    return;
  }

  if (action === "mark-notifications-read") {
    state.notifications.forEach((notification) => {
      notification.unread = false;
      notification.is_read = true;
      notification.read_at = new Date().toISOString();
    });
    renderNotifications();
    showToast("Notifications marquées comme lues.");
    if (api?.currentUserId()) {
      api.markNotificationsRead().catch((error) => {
        console.warn("[Spotiz] Synchronisation lecture notifications impossible :", error.message);
        showToast("Notifications lues ici, synchronisation Supabase en attente.");
      });
    }
    return;
  }

  if (action === "checkin") {
    if (!requireSession()) return;
    await api.checkIn(button.dataset.barId || state.selectedBarId);
    showToast("Check-in enregistré.");
    return;
  }

  if (action === "join-event") {
    if (!requireSession()) return;
    await api.joinEvent(button.dataset.eventId);
    showToast("Participation ajoutée.");
    return;
  }

  if (action === "save-profile") {
    if (!requireSession()) return;
    const secretMode = document.querySelector("[data-action='secret-mode']")?.checked || false;
    state.profile = await api.updateProfile({ secret_mode: secretMode });
    renderProfile();
    showToast("Profil enregistré.");
    return;
  }

  if (action === "register-demo") {
    const form = document.querySelector("[data-auth-form]");
    const email = form.elements.email.value.trim();
    const password = form.elements.password.value;
    if (!email || !password) {
      showToast("Entre un email et un mot de passe.");
      return;
    }

    await api.signUp(email, password, {
      username: email.split("@")[0],
      account_type: "user"
    });
    document.querySelector("[data-auth-status]").textContent = "Compte créé. Vérifie ta boîte mail pour valider ton adresse.";
    showToast("Email de validation envoyé.");
    return;
  }

  if (action === "google-login") {
    api?.signInWithGoogle("user");
    return;
  }

  if (action === "google-register") {
    const accountType = document.querySelector("[data-register-form] select[name='account_type']")?.value || "user";
    api?.signInWithGoogle(accountType);
    return;
  }

  if (action === "logout") {
    await api?.signOut();
    state.profile = null;
    showToast("Tu es déconnecté.");
    setRoute("auth");
    return;
  }

  const messages = {
    locate: "Position mise à jour autour de Lille.",
    directions: "Itineraire pret a ouvrir.",
    "call-bar": "Appel du bar pret.",
    "open-instagram": "Instagram du bar pret.",
    "open-website": "Site du bar pret.",
    "quick-create": "Création de sortie prête.",
    "rate-creator": "Note organisateur prête à envoyer.",
    "invite-friend": "Lien d'invitation préparé.",
    "create-group": "Nouveau groupe prêt à créer.",
    "new-message": "Choisis un ami pour lancer une nouvelle discussion.",
    "share-location": "Position partageable prête.",
    "mute-conversation": "Conversation mise en sourdine.",
    "report-conversation": "Signalement prêt à envoyer.",
    "create-pro-event": "Événement pro prêt à publier.",
    "create-reward": "Création de récompense prête.",
    "scan-reward": "Scanner QR prêt à brancher.",
    "follow-bar": "Bar ajouté à tes favoris."
  };

  if (messages[action]) showToast(messages[action]);
}

document.addEventListener("click", async (event) => {
  const routeButton = event.target.closest("[data-route]");
  if (routeButton) {
    event.preventDefault();
    setRoute(routeButton.dataset.route);
    return;
  }

  const actionButton = event.target.closest("[data-action]");
  if (!actionButton) return;

  try {
    await handleAction(actionButton);
  } catch (error) {
    showToast(error.message || "Action impossible pour le moment.");
  }
});

document.querySelector("[data-filters]").addEventListener("click", (event) => {
  const filter = event.target.closest("[data-filter]");
  if (!filter) return;

  document.querySelectorAll("[data-filter]").forEach((button) => {
    button.classList.toggle("is-active", button === filter);
  });
  renderEvents(filter.dataset.filter);
});

document.querySelector("[data-conversation-search]")?.addEventListener("input", (event) => {
  state.conversationSearch = event.currentTarget.value;
  renderMessages();
});

document.querySelector("[data-message-form]").addEventListener("submit", async (event) => {
  event.preventDefault();
  const input = event.currentTarget.elements.message;
  const text = input.value.trim();
  if (!text) return;

  try {
    const conversation = state.conversations[state.selectedConversationIndex];
    if (!conversation) return;

    if (api?.currentUserId() && conversation.partner_id) {
      await api.sendMessage(conversation.partner_id, text);
    }

    const now = new Date().toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit" });
    conversation.messages = conversation.messages || [];
    conversation.messages.push({ text, me: true, time: now });
    conversation.text = text;
    conversation.time = now;
    input.value = "";
    renderMessages();
    showToast("Message envoyé.");
  } catch (error) {
    showToast(error.message || "Message non envoyé.");
  }
});

document.querySelector("[data-auth-form]").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const status = document.querySelector("[data-auth-status]");

  try {
    await api.signIn(form.elements.email.value.trim(), form.elements.password.value);
    status.textContent = "Connexion réussie.";
    showToast("Bienvenue sur Spotiz.");
    await loadPrivateData();
    setRoute("map");
  } catch (error) {
    status.textContent = error.message || "Connexion impossible.";
  }
});

document.querySelector("[data-register-form]")?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const status = document.querySelector("[data-register-status]");

  try {
    await api.signUp(form.elements.email.value.trim(), form.elements.password.value, {
      username: form.elements.username.value.trim(),
      account_type: form.elements.account_type.value,
      professional_kind: form.elements.account_type.value === "user" ? null : form.elements.account_type.value
    });
    status.textContent = "Compte créé. Vérifie ta boîte mail pour valider ton adresse.";
    showToast("Email de validation envoyé.");
  } catch (error) {
    status.textContent = error.message || "Inscription impossible.";
  }
});

document.querySelector("[data-forgot-form]")?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const status = document.querySelector("[data-forgot-status]");

  try {
    await api.resetPassword(form.elements.email.value.trim());
    status.textContent = "Lien envoyé si cette adresse existe.";
    showToast("Email de réinitialisation envoyé.");
  } catch (error) {
    status.textContent = error.message || "Envoi impossible.";
  }
});

document.querySelector("[data-create-event-form]")?.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!requireSession()) return;

  const form = event.currentTarget;
  const status = document.querySelector("[data-create-event-status]");
  const startAt = new Date(form.elements.start_at.value);
  const expiresAt = new Date(startAt);
  expiresAt.setHours(expiresAt.getHours() + 6);

  try {
    await api.createEvent({
      title: form.elements.title.value.trim(),
      place_name: form.elements.place_name.value.trim(),
      description: form.elements.description.value.trim(),
      visibility: form.elements.visibility.value,
      start_at: startAt.toISOString(),
      expires_at: expiresAt.toISOString(),
      status: "published",
      is_active: true
    });
    status.textContent = "Sortie publiée.";
    showToast("Sortie créée.");
    setRoute("events");
  } catch (error) {
    status.textContent = error.message || "Publication impossible.";
  }
});

document.querySelector("[data-register-bar-form]")?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const status = document.querySelector("[data-register-bar-status]");
  status.textContent = "Demande prête à envoyer côté Supabase.";
  showToast("Fiche pro préparée.");
});

window.addEventListener("popstate", () => setRoute(routeFromLocation(), false));

const savedTheme = localStorage.getItem("spotiz-theme");
if (savedTheme === "dark") {
  app.dataset.theme = "dark";
  document.querySelector("[data-action='toggle-theme']").textContent = "☀";
}

state.bars = state.bars.map(mapBar);
renderAll();
if (restoreOAuthSessionFromHash()) {
  setRoute("map", false);
} else {
  setRoute(routeFromLocation(), false);
}
loadSupabaseData();
