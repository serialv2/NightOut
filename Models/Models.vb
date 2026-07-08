Option Strict Off
Option Explicit On

Imports Newtonsoft.Json

Namespace Models

    Public Class Profile
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("username")> Public Property Username As String
        <JsonProperty("display_name")> Public Property DisplayName As String
        <JsonProperty("avatar_url")> Public Property AvatarUrl As String
        <JsonProperty("city_id")> Public Property CityId As String
        <JsonProperty("birthdate")> Public Property Birthdate As DateTime?
        <JsonProperty("gender")> Public Property Gender As String
        <JsonProperty("is_pro")> Public Property IsPro As Boolean
        <JsonProperty("is_verified")> Public Property IsVerified As Boolean
        <JsonProperty("is_admin")> Public Property IsAdmin As Boolean
        <JsonProperty("account_type")> Public Property AccountType As String
        <JsonProperty("professional_status")> Public Property ProfessionalStatus As String
        <JsonProperty("professional_kind")> Public Property ProfessionalKind As String
        <JsonProperty("is_banned")> Public Property IsBanned As Boolean
        <JsonProperty("ban_reason")> Public Property BanReason As String
        <JsonProperty("banned_at")> Public Property BannedAt As DateTime?
        <JsonProperty("banned_by")> Public Property BannedBy As String
        <JsonProperty("moderation_warning_count")> Public Property ModerationWarningCount As Integer
        <JsonProperty("language")> Public Property Language As String
        <JsonProperty("nights_out")> Public Property NightsOut As Integer
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime

        <JsonIgnore>
        Public ReadOnly Property NameDisplay As String
            Get
                If Not String.IsNullOrWhiteSpace(DisplayName) Then Return DisplayName
                Return If(Username, "—")
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property ProKindLabel As String
            Get
                Dim k = If(Not String.IsNullOrEmpty(ProfessionalKind), ProfessionalKind, AccountType)
                If k = "organizer" Then Return "Organisateur"
                Return "Établissement"
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property ProStatusLabel As String
            Get
                Select Case ProfessionalStatus
                    Case "approved" : Return "Validé"
                    Case "partner" : Return "Partenaire"
                    Case "suspended" : Return "Suspendu"
                    Case "rejected" : Return "Refusé"
                    Case "pending" : Return "En attente"
                    Case Else : Return "—"
                End Select
            End Get
        End Property
    End Class

    Public Class Bar
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("owner_id")> Public Property OwnerId As String
        <JsonProperty("city_id")> Public Property CityId As String
        <JsonProperty("name")> Public Property Name As String
        <JsonProperty("address")> Public Property Address As String
        <JsonProperty("latitude")> Public Property Latitude As Double
        <JsonProperty("longitude")> Public Property Longitude As Double
        <JsonProperty("description")> Public Property Description As String
        <JsonProperty("category")> Public Property Category As String
        <JsonProperty("phone")> Public Property Phone As String
        <JsonProperty("website")> Public Property Website As String
        <JsonProperty("instagram")> Public Property Instagram As String
        <JsonProperty("is_active")> Public Property IsActive As Boolean
        <JsonProperty("total_present")> Public Property TotalPresent As Integer
        <JsonProperty("professional_account_id")> Public Property ProfessionalAccountId As String
        <JsonProperty("logo_url")> Public Property LogoUrl As String
        <JsonProperty("cover_url")> Public Property CoverUrl As String
        <JsonProperty("is_verified")> Public Property IsVerified As Boolean
        <JsonProperty("is_premium")> Public Property IsPremium As Boolean
        <JsonProperty("status")> Public Property Status As String
        <JsonProperty("address_city_name")> Public Property AddressCityName As String
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime

        <JsonIgnore>
        Public ReadOnly Property StatusLabel As String
            Get
                Select Case Status
                    Case "approved" : Return "Validé"
                    Case "rejected" : Return "Refusé"
                    Case Else : Return "En attente"
                End Select
            End Get
        End Property
    End Class

    Public Class ProfessionalAccount
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("user_id")> Public Property UserId As String
        <JsonProperty("kind")> Public Property Kind As String
        <JsonProperty("status")> Public Property Status As String
        <JsonProperty("display_name")> Public Property DisplayName As String
        <JsonProperty("legal_name")> Public Property LegalName As String
        <JsonProperty("phone")> Public Property Phone As String
        <JsonProperty("public_email")> Public Property PublicEmail As String
        <JsonProperty("description")> Public Property Description As String
        <JsonProperty("address")> Public Property Address As String
        <JsonProperty("city_name")> Public Property CityName As String
        <JsonProperty("latitude")> Public Property Latitude As Double?
        <JsonProperty("longitude")> Public Property Longitude As Double?
        <JsonProperty("logo_url")> Public Property LogoUrl As String
        <JsonProperty("cover_url")> Public Property CoverUrl As String
        <JsonProperty("rejection_reason")> Public Property RejectionReason As String
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime

        <JsonIgnore>
        Public ReadOnly Property KindLabel As String
            Get
                If Kind = "organizer" Then Return "Organisateur"
                Return "Établissement"
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property StatusLabel As String
            Get
                Select Case Status
                    Case "approved" : Return "Validé"
                    Case "partner" : Return "Partenaire"
                    Case "suspended" : Return "Suspendu"
                    Case "rejected" : Return "Refusé"
                    Case Else : Return "En attente"
                End Select
            End Get
        End Property
    End Class

    Public Class OfficialEvent
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("professional_account_id")> Public Property ProfessionalAccountId As String
        <JsonProperty("bar_id")> Public Property BarId As String
        <JsonProperty("city_id")> Public Property CityId As String
        <JsonProperty("title")> Public Property Title As String
        <JsonProperty("description")> Public Property Description As String
        <JsonProperty("flyer_url")> Public Property FlyerUrl As String
        <JsonProperty("start_at")> Public Property StartAt As DateTime
        <JsonProperty("end_at")> Public Property EndAt As DateTime?
        <JsonProperty("max_participants")> Public Property MaxParticipants As Integer?
        <JsonProperty("latitude")> Public Property Latitude As Double?
        <JsonProperty("longitude")> Public Property Longitude As Double?
        <JsonProperty("status")> Public Property Status As String
        <JsonProperty("is_active")> Public Property IsActive As Boolean
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime

        ' Rempli côté service (non mappé)
        <JsonIgnore> Public Property BarName As String

        <JsonIgnore>
        Public ReadOnly Property StatusLabel As String
            Get
                Select Case Status
                    Case "published" : Return "Publié"
                    Case "cancelled" : Return "Annulé"
                    Case "archived" : Return "Archivé"
                    Case Else : Return "Brouillon"
                End Select
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property EffectiveEnd As DateTime
            Get
                If EndAt.HasValue Then Return EndAt.Value
                Return StartAt.AddHours(8)
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property IsLiveNow As Boolean
            Get
                Dim now = DateTime.UtcNow
                Return StartAt.ToUniversalTime() <= now AndAlso EffectiveEnd.ToUniversalTime() >= now
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property DateLabel As String
            Get
                Return StartAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            End Get
        End Property
    End Class

    Public Class EphemeralEvent
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("creator_id")> Public Property CreatorId As String
        <JsonProperty("bar_id")> Public Property BarId As String
        <JsonProperty("city_id")> Public Property CityId As String
        <JsonProperty("title")> Public Property Title As String
        <JsonProperty("description")> Public Property Description As String
        <JsonProperty("place_name")> Public Property PlaceName As String
        <JsonProperty("address")> Public Property Address As String
        <JsonProperty("image_url")> Public Property ImageUrl As String
        <JsonProperty("category")> Public Property Category As String
        <JsonProperty("visibility")> Public Property Visibility As String
        <JsonProperty("group_id")> Public Property GroupId As String
        <JsonProperty("start_at")> Public Property StartAt As DateTime
        <JsonProperty("expires_at")> Public Property ExpiresAt As DateTime
        <JsonProperty("latitude")> Public Property Latitude As Double?
        <JsonProperty("longitude")> Public Property Longitude As Double?
        <JsonProperty("status")> Public Property Status As String
        <JsonProperty("is_active")> Public Property IsActive As Boolean
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime

        <JsonIgnore> Public Property BarName As String
        <JsonIgnore> Public Property CreatorName As String

        <JsonIgnore>
        Public ReadOnly Property DateLabel As String
            Get
                Return StartAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property StatusLabel As String
            Get
                Select Case Status
                    Case "published" : Return "Publié"
                    Case "cancelled" : Return "Annulé"
                    Case "archived" : Return "Archivé"
                    Case Else : Return "Brouillon"
                End Select
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property IsLiveNow As Boolean
            Get
                Dim now = DateTime.UtcNow
                Return StartAt.ToUniversalTime() <= now AndAlso ExpiresAt.ToUniversalTime() >= now
            End Get
        End Property
    End Class

    Public Class AppSetting
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("setting_key")> Public Property SettingKey As String
        <JsonProperty("setting_value")> Public Property SettingValue As String
        <JsonProperty("label")> Public Property Label As String
        <JsonProperty("description")> Public Property Description As String
        <JsonProperty("updated_at")> Public Property UpdatedAt As DateTime?

        <JsonIgnore>
        Public ReadOnly Property ValueAsInteger As Integer
            Get
                Dim value As Integer
                If Integer.TryParse(SettingValue, value) Then Return value
                Return 0
            End Get
        End Property
    End Class

    Public Class Category
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("name")> Public Property Name As String
        <JsonProperty("slug")> Public Property Slug As String
        <JsonProperty("icon")> Public Property Icon As String
        <JsonProperty("color")> Public Property Color As String
        <JsonProperty("sort_order")> Public Property SortOrder As Integer
    End Class

    Public Class City
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("name")> Public Property Name As String
        <JsonProperty("slug")> Public Property Slug As String
        <JsonProperty("is_active")> Public Property IsActive As Boolean
        <JsonProperty("latitude")> Public Property Latitude As Double
        <JsonProperty("longitude")> Public Property Longitude As Double
        <JsonProperty("zoom_level")> Public Property ZoomLevel As Integer
        <JsonProperty("radius_km")> Public Property RadiusKm As Integer
    End Class

    ''' <summary>Conteneur de statistiques pour le tableau de bord.</summary>
    Public Class DashboardStats
        Public Property BarsTotal As Integer
        Public Property BarsApproved As Integer
        Public Property BarsPending As Integer
        Public Property BarsRejected As Integer
        Public Property EventsTotal As Integer
        Public Property EventsLive As Integer
        Public Property EventsUpcoming As Integer
        Public Property ProTotal As Integer
        Public Property ProPending As Integer
        Public Property UsersTotal As Integer
        Public Property UsersFemale As Integer
        Public Property UsersMale As Integer
        Public Property UsersGenderUnknown As Integer
        Public Property CheckinsTotal As Integer
        Public Property CheckinsFemale As Integer
        Public Property CheckinsMale As Integer
        Public Property CheckinsGenderUnknown As Integer
        Public Property BarProfileViewsTotal As Integer
        Public Property FollowersTotal As Integer
        ' Top établissements par nombre d'abonnés (nom -> count)
        Public Property TopBars As New List(Of KeyValuePair(Of String, Integer))
        ' Répartition des bars par catégorie (libellé -> count)
        Public Property BarsByCategory As New List(Of KeyValuePair(Of String, Integer))
    End Class

    ''' <summary>Horaire d'ouverture d'un bar pour un jour (1 = lundi … 7 = dimanche).</summary>
    Public Class BarOpeningHour
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("bar_id")> Public Property BarId As String
        <JsonProperty("day_of_week")> Public Property DayOfWeek As Integer
        <JsonProperty("open_time")> Public Property OpenTime As String   ' "HH:mm:ss" ou Nothing
        <JsonProperty("close_time")> Public Property CloseTime As String ' "HH:mm:ss" ou Nothing
        <JsonProperty("is_closed")> Public Property IsClosed As Boolean

        <JsonIgnore>
        Public Shared ReadOnly Property DayNames As String()
            Get
                Return New String() {"Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"}
            End Get
        End Property
    End Class


    Public Class BarClaimRequest
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("bar_id")> Public Property BarId As String
        <JsonProperty("requester_user_id")> Public Property RequesterUserId As String
        <JsonProperty("professional_account_id")> Public Property ProfessionalAccountId As String
        <JsonProperty("contact_name")> Public Property ContactName As String
        <JsonProperty("role")> Public Property Role As String
        <JsonProperty("phone")> Public Property Phone As String
        <JsonProperty("proof_message")> Public Property ProofMessage As String
        <JsonProperty("proof_file_url")> Public Property ProofFileUrl As String
        <JsonProperty("status")> Public Property Status As String
        <JsonProperty("admin_note")> Public Property AdminNote As String
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime
        <JsonProperty("reviewed_at")> Public Property ReviewedAt As DateTime?

        <JsonIgnore> Public Property BarName As String
        <JsonIgnore> Public Property BarAddress As String
        <JsonIgnore> Public Property ProName As String
        <JsonIgnore> Public Property RequesterName As String

        <JsonIgnore>
        Public ReadOnly Property StatusLabel As String
            Get
                Select Case Status
                    Case "approved" : Return "Validée"
                    Case "rejected" : Return "Refusée"
                    Case "cancelled" : Return "Annulée"
                    Case Else : Return "En attente"
                End Select
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property CreatedLabel As String
            Get
                Return CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            End Get
        End Property
    End Class


    Public Class PointRule
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("rule_key")> Public Property RuleKey As String
        <JsonProperty("label")> Public Property Label As String
        <JsonProperty("description")> Public Property Description As String
        <JsonProperty("amount")> Public Property Amount As Integer
        <JsonProperty("is_active")> Public Property IsActive As Boolean
        <JsonProperty("sort_order")> Public Property SortOrder As Integer
        <JsonProperty("updated_at")> Public Property UpdatedAt As DateTime

        <JsonIgnore>
        Public ReadOnly Property AmountLabel As String
            Get
                If Amount >= 0 Then Return "+" & Amount.ToString()
                Return Amount.ToString()
            End Get
        End Property
    End Class

    Public Class MessageReport
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("reporter_id")> Public Property ReporterId As String
        <JsonProperty("reported_user_id")> Public Property ReportedUserId As String
        <JsonProperty("direct_message_id")> Public Property DirectMessageId As String
        <JsonProperty("conversation_partner_id")> Public Property ConversationPartnerId As String
        <JsonProperty("reason")> Public Property Reason As String
        <JsonProperty("message_content_snapshot")> Public Property MessageContentSnapshot As String
        <JsonProperty("status")> Public Property Status As String
        <JsonProperty("admin_note")> Public Property AdminNote As String
        <JsonProperty("reviewed_by")> Public Property ReviewedBy As String
        <JsonProperty("reviewed_at")> Public Property ReviewedAt As DateTime?
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime

        <JsonIgnore> Public Property ReporterName As String
        <JsonIgnore> Public Property ReportedUserName As String
        <JsonIgnore> Public Property ReportedUserWarningCount As Integer
        <JsonIgnore> Public Property ReportedUserIsBanned As Boolean

        <JsonIgnore>
        Public ReadOnly Property ReasonLabel As String
            Get
                Select Case Reason
                    Case "harassment" : Return "Harcelement"
                    Case "spam" : Return "Spam"
                    Case "inappropriate" : Return "Contenu inapproprie"
                    Case "threat" : Return "Menace"
                    Case Else : Return "Autre"
                End Select
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property StatusLabel As String
            Get
                Select Case Status
                    Case "reviewed" : Return "Traite"
                    Case "dismissed" : Return "Ignore"
                    Case "action_taken" : Return "Action prise"
                    Case Else : Return "En attente"
                End Select
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property CreatedLabel As String
            Get
                Return CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            End Get
        End Property
    End Class

    Public Class ModerationWarning
        <JsonProperty("id")> Public Property Id As String
        <JsonProperty("user_id")> Public Property UserId As String
        <JsonProperty("report_id")> Public Property ReportId As String
        <JsonProperty("direct_message_id")> Public Property DirectMessageId As String
        <JsonProperty("reason")> Public Property Reason As String
        <JsonProperty("message")> Public Property Message As String
        <JsonProperty("created_by")> Public Property CreatedBy As String
        <JsonProperty("created_at")> Public Property CreatedAt As DateTime
    End Class

End Namespace
