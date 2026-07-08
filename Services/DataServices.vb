Option Strict Off
Option Explicit On

Imports System.Linq
Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Models

Namespace Services

    ' ════════════════════════════════════════════════════════════
    '  BARS
    ' ════════════════════════════════════════════════════════════
    Public Module BarService

        Public Async Function GetAllAsync() As Task(Of List(Of Bar))
            Return Await SupabaseClient.GetListAsync(Of Bar)(
                "bars?select=*&order=created_at.desc")
        End Function

        Public Async Function GetByStatusAsync(status As String) As Task(Of List(Of Bar))
            Return Await SupabaseClient.GetListAsync(Of Bar)(
                $"bars?status=eq.{status}&select=*&order=created_at.desc")
        End Function

        Public Async Function GetByIdAsync(barId As String) As Task(Of Bar)
            Dim list = Await SupabaseClient.GetListAsync(Of Bar)(
                $"bars?id=eq.{barId}&select=*&limit=1")
            Return If(list IsNot Nothing AndAlso list.Count > 0, list(0), Nothing)
        End Function

        ''' <summary>
        ''' Établissements d'un patron : reliés au compte pro (professional_account_id)
        ''' OU appartenant au même utilisateur propriétaire (owner_id).
        ''' </summary>
        Public Async Function GetForProAccountAsync(proAccountId As String, ownerUserId As String) As Task(Of List(Of Bar))
            Dim parts As New List(Of String)()
            If Not String.IsNullOrEmpty(proAccountId) Then parts.Add($"professional_account_id.eq.{proAccountId}")
            If Not String.IsNullOrEmpty(ownerUserId) Then parts.Add($"owner_id.eq.{ownerUserId}")
            If parts.Count = 0 Then Return New List(Of Bar)()
            Dim orExpr = "or=(" & String.Join(",", parts) & ")"
            Return Await SupabaseClient.GetListAsync(Of Bar)(
                $"bars?{orExpr}&select=*&order=name.asc")
        End Function

        ''' <summary>Met à jour l'ensemble des champs éditables d'un bar.</summary>
        Public Async Function UpdateAsync(b As Bar) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("name", b.Name),
                New JProperty("category", If(b.Category, CType(Nothing, JToken))),
                New JProperty("address", If(b.Address, CType(Nothing, JToken))),
                New JProperty("address_city_name", If(b.AddressCityName, CType(Nothing, JToken))),
                New JProperty("description", If(b.Description, CType(Nothing, JToken))),
                New JProperty("phone", If(b.Phone, CType(Nothing, JToken))),
                New JProperty("website", If(b.Website, CType(Nothing, JToken))),
                New JProperty("instagram", If(b.Instagram, CType(Nothing, JToken))),
                New JProperty("latitude", b.Latitude),
                New JProperty("longitude", b.Longitude),
                New JProperty("status", b.Status),
                New JProperty("is_active", b.IsActive),
                New JProperty("is_premium", b.IsPremium),
                New JProperty("is_verified", b.IsVerified))
            Return Await SupabaseClient.PatchAsync("bars", $"id=eq.{b.Id}", body)
        End Function

        Public Async Function ApproveAsync(barId As String) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("status", "approved"),
                New JProperty("is_active", True),
                New JProperty("is_verified", True))
            Return Await SupabaseClient.PatchAsync("bars", $"id=eq.{barId}", body)
        End Function

        Public Async Function RejectAsync(barId As String) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("status", "rejected"),
                New JProperty("is_active", False))
            Return Await SupabaseClient.PatchAsync("bars", $"id=eq.{barId}", body)
        End Function

        Public Async Function SetActiveAsync(barId As String, active As Boolean) As Task(Of Boolean)
            Dim body = New JObject(New JProperty("is_active", active))
            Return Await SupabaseClient.PatchAsync("bars", $"id=eq.{barId}", body)
        End Function

        Public Async Function SetPremiumAsync(barId As String, premium As Boolean) As Task(Of Boolean)
            Dim body = New JObject(New JProperty("is_premium", premium))
            Return Await SupabaseClient.PatchAsync("bars", $"id=eq.{barId}", body)
        End Function

        Public Async Function DeleteAsync(barId As String) As Task(Of Boolean)
            Return Await SupabaseClient.DeleteAsync("bars", $"id=eq.{barId}")
        End Function

    End Module

    ' ════════════════════════════════════════════════════════════
    '  DEMANDES DE RÉCUPÉRATION D'ÉTABLISSEMENT
    ' ════════════════════════════════════════════════════════════
    Public Module BarClaimService

        Public Async Function GetAllAsync(Optional status As String = Nothing) As Task(Of List(Of BarClaimRequest))
            Dim q As String
            If String.IsNullOrWhiteSpace(status) OrElse status = "all" Then
                q = "bar_claim_requests?select=*&order=created_at.desc"
            Else
                q = $"bar_claim_requests?status=eq.{status}&select=*&order=created_at.desc"
            End If

            Dim requests = Await SupabaseClient.GetListAsync(Of BarClaimRequest)(q)
            Await EnrichAsync(requests)
            Return requests
        End Function

        Private Async Function EnrichAsync(requests As List(Of BarClaimRequest)) As Task
            If requests Is Nothing OrElse requests.Count = 0 Then Return

            Try
                Dim bars = Await BarService.GetAllAsync()
                Dim barMap = bars.GroupBy(Function(b) b.Id).ToDictionary(Function(g) g.Key, Function(g) g.First())
                For Each r In requests
                    If Not String.IsNullOrEmpty(r.BarId) AndAlso barMap.ContainsKey(r.BarId) Then
                        r.BarName = barMap(r.BarId).Name
                        r.BarAddress = barMap(r.BarId).Address
                    End If
                Next
            Catch
            End Try

            Try
                Dim pros = Await ProService.GetAllAsync()
                Dim proMap = pros.GroupBy(Function(p) p.Id).ToDictionary(Function(g) g.Key, Function(g) g.First())
                For Each r In requests
                    If Not String.IsNullOrEmpty(r.ProfessionalAccountId) AndAlso proMap.ContainsKey(r.ProfessionalAccountId) Then
                        Dim p = proMap(r.ProfessionalAccountId)
                        r.ProName = If(Not String.IsNullOrWhiteSpace(p.DisplayName), p.DisplayName, If(p.LegalName, p.Id))
                    End If
                Next
            Catch
            End Try

            Try
                Dim users = Await UserService.GetAllAsync()
                Dim userMap = users.GroupBy(Function(u) u.Id).ToDictionary(Function(g) g.Key, Function(g) g.First())
                For Each r In requests
                    If Not String.IsNullOrEmpty(r.RequesterUserId) AndAlso userMap.ContainsKey(r.RequesterUserId) Then
                        r.RequesterName = userMap(r.RequesterUserId).NameDisplay
                    End If
                Next
            Catch
            End Try
        End Function

        Public Async Function ApproveAsync(requestId As String, Optional adminNote As String = Nothing) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("p_request_id", requestId),
                New JProperty("p_admin_note", If(adminNote, CType(Nothing, JToken))))
            Return Await SupabaseClient.RpcAsync("admin_approve_bar_claim", body)
        End Function

        Public Async Function RejectAsync(requestId As String, Optional adminNote As String = Nothing) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("p_request_id", requestId),
                New JProperty("p_admin_note", If(adminNote, CType(Nothing, JToken))))
            Return Await SupabaseClient.RpcAsync("admin_reject_bar_claim", body)
        End Function

    End Module


    ' ════════════════════════════════════════════════════════════
    '  ÉVÉNEMENTS OFFICIELS
    ' ════════════════════════════════════════════════════════════
    Public Module EventService

        Public Async Function GetAllAsync() As Task(Of List(Of OfficialEvent))
            Dim events = Await SupabaseClient.GetListAsync(Of OfficialEvent)(
                "official_events?select=*&order=start_at.desc")

            ' Résolution du nom de bar (jointure côté client, simple et fiable)
            Try
                Dim bars = Await BarService.GetAllAsync()
                Dim map = bars.ToDictionary(Function(b) b.Id, Function(b) b.Name)
                For Each e In events
                    If Not String.IsNullOrEmpty(e.BarId) AndAlso map.ContainsKey(e.BarId) Then
                        e.BarName = map(e.BarId)
                    End If
                Next
            Catch
            End Try

            Return events
        End Function

        Public Async Function GetEphemeralAllAsync() As Task(Of List(Of EphemeralEvent))
            Dim events = Await SupabaseClient.GetListAsync(Of EphemeralEvent)(
                "ephemeral_events?select=*&order=start_at.desc&limit=1000")

            Try
                Dim bars = Await BarService.GetAllAsync()
                Dim barMap = bars.
                    Where(Function(b) Not String.IsNullOrWhiteSpace(b.Id)).
                    GroupBy(Function(b) b.Id).
                    ToDictionary(Function(g) g.Key, Function(g) g.First().Name)

                Dim users = Await UserService.GetAllAsync()
                Dim userMap = users.
                    Where(Function(u) Not String.IsNullOrWhiteSpace(u.Id)).
                    GroupBy(Function(u) u.Id).
                    ToDictionary(Function(g) g.Key, Function(g) g.First().NameDisplay)

                For Each e In events
                    If Not String.IsNullOrEmpty(e.BarId) AndAlso barMap.ContainsKey(e.BarId) Then
                        e.BarName = barMap(e.BarId)
                    End If

                    If Not String.IsNullOrEmpty(e.CreatorId) AndAlso userMap.ContainsKey(e.CreatorId) Then
                        e.CreatorName = userMap(e.CreatorId)
                    End If
                Next
            Catch
            End Try

            Return events
        End Function

        Public Async Function CancelAsync(eventId As String) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("status", "cancelled"),
                New JProperty("is_active", False))
            Return Await SupabaseClient.PatchAsync("official_events", $"id=eq.{eventId}", body)
        End Function

        Public Async Function PublishAsync(eventId As String) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("status", "published"),
                New JProperty("is_active", True))
            Return Await SupabaseClient.PatchAsync("official_events", $"id=eq.{eventId}", body)
        End Function

        Public Async Function DeleteAsync(eventId As String) As Task(Of Boolean)
            Return Await SupabaseClient.DeleteAsync("official_events", $"id=eq.{eventId}")
        End Function

        Public Async Function CancelEphemeralAsync(eventId As String) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("status", "cancelled"),
                New JProperty("is_active", False),
                New JProperty("updated_at", DateTime.UtcNow.ToString("o")))
            Return Await SupabaseClient.PatchAsync("ephemeral_events", $"id=eq.{eventId}", body)
        End Function

        Public Async Function PublishEphemeralAsync(eventId As String) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("status", "published"),
                New JProperty("is_active", True),
                New JProperty("updated_at", DateTime.UtcNow.ToString("o")))
            Return Await SupabaseClient.PatchAsync("ephemeral_events", $"id=eq.{eventId}", body)
        End Function

        Public Async Function DeleteEphemeralAsync(eventId As String) As Task(Of Boolean)
            Return Await SupabaseClient.DeleteAsync("ephemeral_events", $"id=eq.{eventId}")
        End Function

    End Module

    ' ════════════════════════════════════════════════════════════
    '  COMPTES PROFESSIONNELS
    ' ════════════════════════════════════════════════════════════
    Public Module ProService

        Public Async Function GetAllAsync() As Task(Of List(Of ProfessionalAccount))
            Return Await SupabaseClient.GetListAsync(Of ProfessionalAccount)(
                "professional_accounts?select=*&order=created_at.desc")
        End Function

        Public Async Function SetStatusAsync(accountId As String, status As String,
                                              Optional reason As String = Nothing) As Task(Of Boolean)
            Dim body = New JObject(New JProperty("status", status))
            If status = "approved" OrElse status = "partner" Then
                body.Add(New JProperty("approved_at", DateTime.UtcNow.ToString("o")))
            End If
            If Not String.IsNullOrEmpty(reason) Then
                body.Add(New JProperty("rejection_reason", reason))
            End If
            Return Await SupabaseClient.PatchAsync("professional_accounts", $"id=eq.{accountId}", body)
        End Function

    End Module

    ' ════════════════════════════════════════════════════════════
    '  UTILISATEURS
    ' ════════════════════════════════════════════════════════════
    Public Module UserService

        Public Async Function GetAllAsync(Optional search As String = Nothing) As Task(Of List(Of Profile))
            Dim q = "profiles?select=*&order=created_at.desc&limit=500"
            If Not String.IsNullOrWhiteSpace(search) Then
                Dim s = Uri.EscapeDataString(search.Trim())
                q = $"profiles?or=(username.ilike.*{s}*,display_name.ilike.*{s}*)&select=*&order=created_at.desc&limit=200"
            End If
            Return Await SupabaseClient.GetListAsync(Of Profile)(q)
        End Function

        Public Async Function SetVerifiedAsync(userId As String, verified As Boolean) As Task(Of Boolean)
            Dim body = New JObject(New JProperty("is_verified", verified))
            Return Await SupabaseClient.PatchAsync("profiles", $"id=eq.{userId}", body)
        End Function

        Public Async Function SetAdminAsync(userId As String, admin As Boolean) As Task(Of Boolean)
            Dim body = New JObject(New JProperty("is_admin", admin))
            Return Await SupabaseClient.PatchAsync("profiles", $"id=eq.{userId}", body)
        End Function

    End Module

    ' ════════════════════════════════════════════════════════════
    '  PATRONS (profils establishment / organizer / is_pro)
    ' ════════════════════════════════════════════════════════════
    Public Module ProOwnerService

        ''' <summary>Profils considérés « pros » : establishment, organizer, ou is_pro.</summary>
        Public Async Function GetAllAsync() As Task(Of List(Of Profile))
            Return Await SupabaseClient.GetListAsync(Of Profile)(
                "profiles?or=(account_type.eq.establishment,account_type.eq.organizer,is_pro.eq.true)" &
                "&select=*&order=created_at.desc")
        End Function

        ''' <summary>Met à jour le statut pro sur le profil (pending/approved/partner/suspended/rejected).</summary>
        Public Async Function SetStatusAsync(profileId As String, status As String) As Task(Of Boolean)
            Dim body = New JObject(New JProperty("professional_status", status))
            ' Un patron validé/partenaire est aussi marqué vérifié
            If status = "approved" OrElse status = "partner" Then
                body.Add(New JProperty("is_verified", True))
            End If
            Return Await SupabaseClient.PatchAsync("profiles", $"id=eq.{profileId}", body)
        End Function

    End Module

    ' ════════════════════════════════════════════════════════════
    '  CATÉGORIES / VILLES
    ' ════════════════════════════════════════════════════════════
    Public Module RefService

        Public Async Function GetCategoriesAsync() As Task(Of List(Of Category))
            Return Await SupabaseClient.GetListAsync(Of Category)(
                "categories?select=*&order=sort_order.asc")
        End Function

        Public Async Function GetCitiesAsync() As Task(Of List(Of City))
            Return Await SupabaseClient.GetListAsync(Of City)(
                "cities?select=*&order=name.asc")
        End Function

    End Module

    ' ════════════════════════════════════════════════════════════
    '  HORAIRES D'OUVERTURE
    ' ════════════════════════════════════════════════════════════
    Public Module HoursService

        ''' <summary>Renvoie les horaires d'un bar, triés par jour (1 = lundi … 7 = dimanche).</summary>
        Public Async Function GetForBarAsync(barId As String) As Task(Of List(Of BarOpeningHour))
            Return Await SupabaseClient.GetListAsync(Of BarOpeningHour)(
                $"bar_opening_hours?bar_id=eq.{barId}&select=*&order=day_of_week.asc")
        End Function

        ''' <summary>
        ''' Enregistre les 7 jours en un seul upsert (conflit sur bar_id,day_of_week).
        ''' Si un jour est fermé, open_time/close_time sont mis à NULL.
        ''' </summary>
        Public Async Function SaveAsync(barId As String, hours As List(Of BarOpeningHour)) As Task(Of Boolean)
            If hours Is Nothing OrElse hours.Count = 0 Then Return True
            Dim arr As New JArray()
            For Each h In hours
                Dim o As New JObject()
                o("bar_id") = barId
                o("day_of_week") = h.DayOfWeek
                o("is_closed") = h.IsClosed
                If h.IsClosed OrElse String.IsNullOrEmpty(h.OpenTime) Then
                    o("open_time") = CType(Nothing, JToken)
                Else
                    o("open_time") = h.OpenTime
                End If
                If h.IsClosed OrElse String.IsNullOrEmpty(h.CloseTime) Then
                    o("close_time") = CType(Nothing, JToken)
                Else
                    o("close_time") = h.CloseTime
                End If
                o("updated_at") = DateTime.UtcNow.ToString("o")
                arr.Add(o)
            Next
            Return Await SupabaseClient.UpsertAsync("bar_opening_hours", arr, "bar_id,day_of_week")
        End Function

    End Module


    ' ════════════════════════════════════════════════════════════
    '  RÈGLES DE POINTS
    ' ════════════════════════════════════════════════════════════
    Public Module PointRuleService

        Public Async Function GetAllAsync() As Task(Of List(Of PointRule))
            Return Await SupabaseClient.GetListAsync(Of PointRule)(
                "point_rules?select=*&order=sort_order.asc,rule_key.asc")
        End Function

        Public Async Function UpdateAsync(rule As PointRule) As Task(Of Boolean)
            If rule Is Nothing OrElse String.IsNullOrWhiteSpace(rule.Id) Then Return False

            Dim body = New JObject(
                New JProperty("label", rule.Label),
                New JProperty("description", If(rule.Description, CType(Nothing, JToken))),
                New JProperty("amount", rule.Amount),
                New JProperty("is_active", rule.IsActive),
                New JProperty("sort_order", rule.SortOrder),
                New JProperty("updated_at", DateTime.UtcNow.ToString("o")))

            Return Await SupabaseClient.PatchAsync("point_rules", $"id=eq.{rule.Id}", body)
        End Function

    End Module

    ' ============================================================================
    '  PARAMETRES APPLICATION
    ' ============================================================================
    Public Module AppSettingService

        Public Const CheckinPresenceMinutesKey As String = "checkin_presence_duration_minutes"

        Public Async Function GetAllAsync() As Task(Of List(Of AppSetting))
            Return Await SupabaseClient.GetListAsync(Of AppSetting)(
                "app_settings?select=*&order=setting_key.asc")
        End Function

        Public Async Function GetCheckinPresenceMinutesAsync() As Task(Of Integer)
            Dim settings = Await GetAllAsync()
            Dim row = settings.FirstOrDefault(Function(s) String.Equals(s.SettingKey, CheckinPresenceMinutesKey, StringComparison.OrdinalIgnoreCase))
            If row Is Nothing OrElse row.ValueAsInteger <= 0 Then Return 60
            Return row.ValueAsInteger
        End Function

        Public Async Function SaveCheckinPresenceMinutesAsync(minutes As Integer) As Task(Of Boolean)
            If minutes < 5 OrElse minutes > 1440 Then
                Throw New ArgumentOutOfRangeException(NameOf(minutes), "La duree doit etre comprise entre 5 minutes et 24 heures.")
            End If

            Dim row = New JObject(
                New JProperty("setting_key", CheckinPresenceMinutesKey),
                New JProperty("setting_value", minutes.ToString(Globalization.CultureInfo.InvariantCulture)),
                New JProperty("label", "Duree de presence check-in"),
                New JProperty("description", "Duree pendant laquelle un utilisateur reste present dans un bar apres check-in, sauf check-out manuel ou heartbeat."),
                New JProperty("updated_at", DateTime.UtcNow.ToString("o")))

            Return Await SupabaseClient.UpsertAsync("app_settings", New JArray(row), "setting_key")
        End Function

    End Module

    ' ============================================================================
    '  SIGNALEMENTS DE MESSAGES
    ' ============================================================================
    Public Module MessageReportService

        Public Async Function GetAllAsync(Optional status As String = Nothing) As Task(Of List(Of MessageReport))
            Dim q As String
            If String.IsNullOrWhiteSpace(status) OrElse status = "all" Then
                q = "message_reports?select=*&order=created_at.desc&limit=500"
            Else
                q = $"message_reports?status=eq.{status}&select=*&order=created_at.desc&limit=500"
            End If

            Dim reports = Await SupabaseClient.GetListAsync(Of MessageReport)(q)
            Await EnrichAsync(reports)
            Return reports
        End Function

        Private Async Function EnrichAsync(reports As List(Of MessageReport)) As Task
            If reports Is Nothing OrElse reports.Count = 0 Then Return

            Try
                Dim users = Await UserService.GetAllAsync()
                Dim userMap = users.
                    Where(Function(u) Not String.IsNullOrWhiteSpace(u.Id)).
                    GroupBy(Function(u) u.Id).
                    ToDictionary(Function(g) g.Key, Function(g) g.First())

                For Each r In reports
                    If Not String.IsNullOrWhiteSpace(r.ReporterId) AndAlso userMap.ContainsKey(r.ReporterId) Then
                        r.ReporterName = userMap(r.ReporterId).NameDisplay
                    End If
                    If Not String.IsNullOrWhiteSpace(r.ReportedUserId) AndAlso userMap.ContainsKey(r.ReportedUserId) Then
                        Dim reported = userMap(r.ReportedUserId)
                        r.ReportedUserName = reported.NameDisplay
                        r.ReportedUserWarningCount = reported.ModerationWarningCount
                        r.ReportedUserIsBanned = reported.IsBanned
                    End If
                Next
            Catch
            End Try
        End Function

        Public Async Function SetStatusAsync(reportId As String, status As String, Optional adminNote As String = Nothing) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("status", status),
                New JProperty("admin_note", If(adminNote, CType(Nothing, JToken))),
                New JProperty("reviewed_by", If(SupabaseClient.CurrentUserId, CType(Nothing, JToken))),
                New JProperty("reviewed_at", DateTime.UtcNow.ToString("o")))

            Return Await SupabaseClient.PatchAsync("message_reports", $"id=eq.{reportId}", body)
        End Function

        Public Async Function DismissWithReporterMessageAsync(report As MessageReport, reporterMessage As String) As Task(Of Boolean)
            Await SendAdminDirectMessageAsync(report.ReporterId, reporterMessage)

            Await CreateNotificationAsync(
                report.ReporterId,
                "moderation_report_dismissed",
                "Signalement examine",
                reporterMessage,
                report.Id,
                "message_report")

            Return Await SetStatusAsync(report.Id, "dismissed", reporterMessage)
        End Function

        Public Async Function WarnReportedUserAsync(report As MessageReport, warningMessage As String) As Task(Of Boolean)
            Await CreateWarningAsync(report, warningMessage)
            Await SendAdminDirectMessageAsync(report.ReportedUserId, warningMessage)

            Await CreateNotificationAsync(
                report.ReportedUserId,
                "moderation_warning",
                "Avertissement moderation",
                warningMessage,
                report.Id,
                "message_report")

            Return Await SetStatusAsync(report.Id, "action_taken", "Avertissement envoye a l'utilisateur signale.")
        End Function

        Public Async Function BanReportedUserAsync(report As MessageReport, banReason As String, reporterMessage As String) As Task(Of Boolean)
            Await SendAdminDirectMessageAsync(report.ReportedUserId, banReason)
            Await SendAdminDirectMessageAsync(report.ReporterId, reporterMessage)

            Dim body = New JObject(
                New JProperty("is_banned", True),
                New JProperty("ban_reason", banReason),
                New JProperty("banned_by", If(SupabaseClient.CurrentUserId, CType(Nothing, JToken))),
                New JProperty("banned_at", DateTime.UtcNow.ToString("o")))

            Await SupabaseClient.PatchAsync("profiles", $"id=eq.{report.ReportedUserId}", body)

            Await CreateNotificationAsync(
                report.ReportedUserId,
                "moderation_ban",
                "Compte banni",
                banReason,
                report.Id,
                "message_report")

            Await CreateNotificationAsync(
                report.ReporterId,
                "moderation_report_action_taken",
                "Signalement traite",
                reporterMessage,
                report.Id,
                "message_report")

            Return Await SetStatusAsync(report.Id, "action_taken", "Utilisateur banni. " & reporterMessage)
        End Function

        Private Async Function SendAdminDirectMessageAsync(receiverId As String, message As String) As Task(Of Boolean)
            If String.IsNullOrWhiteSpace(receiverId) OrElse String.IsNullOrWhiteSpace(message) Then Return False

            Dim body = New JObject(
                New JProperty("sender_id", SupabaseClient.CurrentUserId),
                New JProperty("receiver_id", receiverId),
                New JProperty("content", message),
                New JProperty("type", "text"),
                New JProperty("created_at", DateTime.UtcNow.ToString("o")))

            Await SupabaseClient.InsertAsync("direct_messages", body)

            Await CreateNotificationAsync(
                receiverId,
                "private_message",
                "Message de l'admin",
                message,
                Nothing,
                "direct_message")

            Return True
        End Function

        Private Async Function CreateWarningAsync(report As MessageReport, warningMessage As String) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("user_id", report.ReportedUserId),
                New JProperty("report_id", report.Id),
                New JProperty("direct_message_id", report.DirectMessageId),
                New JProperty("reason", report.Reason),
                New JProperty("message", warningMessage),
                New JProperty("created_by", If(SupabaseClient.CurrentUserId, CType(Nothing, JToken))))

            Await SupabaseClient.InsertAsync("moderation_warnings", body)

            Dim currentCount As Integer = 0
            Try
                Dim users = Await SupabaseClient.GetListAsync(Of Profile)(
                    $"profiles?id=eq.{report.ReportedUserId}&select=id,moderation_warning_count&limit=1")
                If users IsNot Nothing AndAlso users.Count > 0 Then currentCount = users(0).ModerationWarningCount
            Catch
            End Try

            Dim profileBody = New JObject(New JProperty("moderation_warning_count", currentCount + 1))
            Await SupabaseClient.PatchAsync("profiles", $"id=eq.{report.ReportedUserId}", profileBody)

            Return True
        End Function

        Private Async Function CreateNotificationAsync(userId As String, notificationType As String, title As String, message As String,
                                                       Optional entityId As String = Nothing, Optional entityType As String = Nothing) As Task(Of Boolean)
            Dim body = New JObject(
                New JProperty("user_id", userId),
                New JProperty("type", notificationType),
                New JProperty("title", title),
                New JProperty("message", message),
                New JProperty("entity_id", If(entityId, CType(Nothing, JToken))),
                New JProperty("entity_type", If(entityType, CType(Nothing, JToken))),
                New JProperty("actor_id", If(SupabaseClient.CurrentUserId, CType(Nothing, JToken))),
                New JProperty("is_read", False))

            Return Await SupabaseClient.InsertAsync("notifications", body)
        End Function

    End Module

End Namespace
