Option Strict Off
Option Explicit On

Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Models
Imports System.Linq

Namespace Services

    Public Class UserActivitySection
        Public Property Title As String
        Public Property Columns As String()
        Public Property Rows As New List(Of String())()
        Public Property ErrorText As String
    End Class

    Public Class UserActivitySnapshot
        Public Property User As Profile
        Public Property Sections As New List(Of UserActivitySection)()
        Public Property Errors As New List(Of String)()
    End Class

    Public Module UserActivityService

        Public Async Function GetAsync(user As Profile) As Task(Of UserActivitySnapshot)
            Dim data As New UserActivitySnapshot With {.User = user}
            If user Is Nothing OrElse String.IsNullOrWhiteSpace(user.Id) Then Return data

            Dim uid = Uri.EscapeDataString(user.Id)
            Dim profiles = Await LoadMapAsync("profiles?select=id,username,display_name&limit=1000", "id", Function(o) DisplayUser(o), data.Errors, "profils")
            Dim bars = Await LoadMapAsync("bars?select=id,name,address&limit=1000", "id", Function(o) Value(o, "name"), data.Errors, "bars")
            Dim friendGroups = Await LoadMapAsync("friend_groups?select=id,name,emoji&limit=1000", "id", Function(o) (Value(o, "emoji", "") & " " & Value(o, "name")).Trim(), data.Errors, "groupes amis")
            Dim oldGroups = Await LoadMapAsync("groups?select=id,name&limit=1000", "id", Function(o) Value(o, "name"), data.Errors, "groupes")

            data.Sections.Add(BuildSummary(user))
            Await AddFriendsAsync(data, uid, user.Id, profiles)
            Await AddGroupsAsync(data, uid, user.Id, profiles, friendGroups, oldGroups)
            Await AddMessagesAsync(data, uid, user.Id, profiles, friendGroups, oldGroups)
            Await AddOutingsAsync(data, uid, user.Id, bars, friendGroups)
            Await AddEventsAsync(data, uid, user.Id, bars)
            Await AddCheckinsAsync(data, uid, bars)
            Await AddModerationAsync(data, uid, user.Id, profiles)

            If data.Errors.Count > 0 Then
                Dim s As New UserActivitySection With {
                    .Title = "Infos techniques",
                    .Columns = New String() {"Section", "Message"}
                }
                For Each errorMessage In data.Errors
                    Dim p = errorMessage.Split(New String() {": "}, 2, StringSplitOptions.None)
                    s.Rows.Add(New String() {p(0), If(p.Length > 1, p(1), errorMessage)})
                Next
                data.Sections.Add(s)
            End If

            Return data
        End Function

        Private Function BuildSummary(user As Profile) As UserActivitySection
            Dim s As New UserActivitySection With {
                .Title = "Résumé",
                .Columns = New String() {"Champ", "Valeur"}
            }
            s.Rows.Add(New String() {"ID", Safe(user.Id)})
            s.Rows.Add(New String() {"Pseudo", Safe(user.Username)})
            s.Rows.Add(New String() {"Nom affiché", Safe(user.NameDisplay)})
            s.Rows.Add(New String() {"Type", Safe(If(user.AccountType, "user"))})
            s.Rows.Add(New String() {"Genre", Safe(user.Gender)})
            s.Rows.Add(New String() {"Vérifié", YesNo(user.IsVerified)})
            s.Rows.Add(New String() {"Admin", YesNo(user.IsAdmin)})
            s.Rows.Add(New String() {"Banni", YesNo(user.IsBanned)})
            s.Rows.Add(New String() {"Avertissements", user.ModerationWarningCount.ToString()})
            s.Rows.Add(New String() {"Inscrit le", DateValue(user.CreatedAt)})
            Return s
        End Function

        Private Async Function AddFriendsAsync(data As UserActivitySnapshot, uid As String, rawUserId As String, profileMap As Dictionary(Of String, String)) As Task
            Dim s As New UserActivitySection With {
                .Title = "Amis",
                .Columns = New String() {"Date", "Utilisateur", "Statut", "Sens"}
            }
            Dim arr = Await TryArrayAsync($"friendships?or=(requester_id.eq.{uid},addressee_id.eq.{uid})&select=*&order=created_at.desc&limit=300", data.Errors, "amis")
            For Each o As JObject In arr
                Dim otherId = If(Value(o, "requester_id") = rawUserId, Value(o, "addressee_id"), Value(o, "requester_id"))
                Dim sens = If(Value(o, "requester_id") = rawUserId, "Demande envoyée", "Demande reçue")
                s.Rows.Add(New String() {DateValue(o, "created_at"), Lookup(profileMap, otherId), Value(o, "status"), sens})
            Next
            data.Sections.Add(s)
        End Function

        Private Async Function AddGroupsAsync(data As UserActivitySnapshot, uid As String, rawUserId As String, profileMap As Dictionary(Of String, String), friendGroups As Dictionary(Of String, String), oldGroups As Dictionary(Of String, String)) As Task
            Dim s As New UserActivitySection With {
                .Title = "Groupes",
                .Columns = New String() {"Date", "Groupe", "Rôle", "Ajouté par", "Source"}
            }

            Dim members = Await TryArrayAsync($"friend_group_members?user_id=eq.{uid}&select=*&order=created_at.desc&limit=300", data.Errors, "groupes amis")
            For Each o As JObject In members
                s.Rows.Add(New String() {DateValue(o, "created_at"), Lookup(friendGroups, Value(o, "group_id")), Value(o, "role", "membre"), Lookup(profileMap, Value(o, "added_by")), "friend_groups"})
            Next

            Dim owned = Await TryArrayAsync($"friend_groups?owner_id=eq.{uid}&select=*&order=created_at.desc&limit=300", data.Errors, "groupes créés")
            For Each o As JObject In owned
                s.Rows.Add(New String() {DateValue(o, "created_at"), (Value(o, "emoji", "") & " " & Value(o, "name")).Trim(), "Créateur", Lookup(profileMap, rawUserId), "friend_groups"})
            Next

            Dim oldMembers = Await TryArrayAsync($"group_members?user_id=eq.{uid}&select=*&order=joined_at.desc&limit=300", data.Errors, "anciens groupes")
            For Each o As JObject In oldMembers
                s.Rows.Add(New String() {DateValue(o, "joined_at"), Lookup(oldGroups, Value(o, "group_id")), If(BoolValue(o, "is_admin"), "Admin", "Membre"), "—", "groups"})
            Next

            data.Sections.Add(s)
        End Function

        Private Async Function AddMessagesAsync(data As UserActivitySnapshot, uid As String, rawUserId As String, profileMap As Dictionary(Of String, String), friendGroups As Dictionary(Of String, String), oldGroups As Dictionary(Of String, String)) As Task
            Dim s As New UserActivitySection With {
                .Title = "Messages",
                .Columns = New String() {"Date", "Type", "Avec / groupe", "Sens", "Contenu"}
            }

            Dim direct = Await TryArrayAsync($"direct_messages?or=(sender_id.eq.{uid},receiver_id.eq.{uid})&select=*&order=created_at.desc&limit=300", data.Errors, "messages directs")
            For Each o As JObject In direct
                Dim mine = Value(o, "sender_id") = rawUserId
                Dim otherId = If(mine, Value(o, "receiver_id"), Value(o, "sender_id"))
                s.Rows.Add(New String() {DateValue(o, "created_at"), Value(o, "type", "text"), Lookup(profileMap, otherId), If(mine, "Envoyé", "Reçu"), ShortText(Value(o, "content"))})
            Next

            Dim groupMessages = Await TryArrayAsync($"friend_group_messages?sender_id=eq.{uid}&select=*&order=created_at.desc&limit=300", data.Errors, "messages groupes")
            For Each o As JObject In groupMessages
                s.Rows.Add(New String() {DateValue(o, "created_at"), Value(o, "message_type", "text"), Lookup(friendGroups, Value(o, "group_id")), "Envoyé", ShortText(Value(o, "message_text"))})
            Next

            Dim oldMessages = Await TryArrayAsync($"messages?sender_id=eq.{uid}&select=*&order=created_at.desc&limit=300", data.Errors, "anciens messages")
            For Each o As JObject In oldMessages
                s.Rows.Add(New String() {DateValue(o, "created_at"), Value(o, "type", "text"), Value(o, "conversation_id"), "Envoyé", ShortText(Value(o, "content"))})
            Next

            data.Sections.Add(s)
        End Function

        Private Async Function AddOutingsAsync(data As UserActivitySnapshot, uid As String, rawUserId As String, bars As Dictionary(Of String, String), friendGroups As Dictionary(Of String, String)) As Task
            Dim s As New UserActivitySection With {
                .Title = "Sorties créées",
                .Columns = New String() {"Date prévue", "Titre", "Lieu", "Groupe", "Créée le"}
            }
            Dim arr = Await TryArrayAsync($"friend_group_outings?created_by=eq.{uid}&select=*&order=created_at.desc&limit=300", data.Errors, "sorties créées")
            For Each o As JObject In arr
                s.Rows.Add(New String() {DateValue(o, "planned_at"), Value(o, "title"), Lookup(bars, Value(o, "bar_id")), Lookup(friendGroups, Value(o, "group_id")), DateValue(o, "created_at")})
            Next
            data.Sections.Add(s)
        End Function

        Private Async Function AddEventsAsync(data As UserActivitySnapshot, uid As String, rawUserId As String, bars As Dictionary(Of String, String)) As Task
            Dim s As New UserActivitySection With {
                .Title = "Événements créés",
                .Columns = New String() {"Début", "Type", "Titre", "Lieu", "Visibilité / statut"}
            }

            Dim eph = Await TryArrayAsync($"ephemeral_events?creator_id=eq.{uid}&select=*&order=start_at.desc&limit=300", data.Errors, "événements éphémères")
            For Each o As JObject In eph
                Dim place = Value(o, "place_name", Lookup(bars, Value(o, "bar_id", "")))
                If place = "—" Then place = Value(o, "address")
                s.Rows.Add(New String() {DateValue(o, "start_at"), "Éphémère", Value(o, "title"), place, Value(o, "visibility") & " / " & Value(o, "status")})
            Next

            Dim pros = Await TryArrayAsync($"professional_accounts?user_id=eq.{uid}&select=id,display_name,status&limit=50", data.Errors, "comptes pro")
            Dim proIds = pros.Select(Function(o) Value(CType(o, JObject), "id")).Where(Function(id) id <> "—").ToList()
            If proIds.Count > 0 Then
                Dim inList = String.Join(",", proIds.Select(Function(id) Uri.EscapeDataString(id)))
                Dim off = Await TryArrayAsync($"official_events?professional_account_id=in.({inList})&select=*&order=start_at.desc&limit=300", data.Errors, "événements officiels")
                For Each o As JObject In off
                    s.Rows.Add(New String() {DateValue(o, "start_at"), "Officiel", Value(o, "title"), Lookup(bars, Value(o, "bar_id")), Value(o, "status")})
                Next
            End If

            data.Sections.Add(s)
        End Function

        Private Async Function AddCheckinsAsync(data As UserActivitySnapshot, uid As String, bars As Dictionary(Of String, String)) As Task
            Dim s As New UserActivitySection With {
                .Title = "Check-ins",
                .Columns = New String() {"Entrée", "Sortie", "Bar", "Actif", "Genre", "Âge", "Tranche"}
            }
            Dim arr = Await TryArrayAsync($"checkins?user_id=eq.{uid}&select=*&order=checked_in_at.desc&limit=500", data.Errors, "check-ins")
            For Each o As JObject In arr
                s.Rows.Add(New String() {
                    DateValue(o, "checked_in_at"),
                    DateValue(o, "checked_out_at"),
                    Lookup(bars, Value(o, "bar_id")),
                    YesNo(BoolValue(o, "is_active")),
                    Value(o, "gender_snapshot"),
                    Value(o, "age_snapshot"),
                    Value(o, "age_band_snapshot")
                })
            Next
            data.Sections.Add(s)
        End Function

        Private Async Function AddModerationAsync(data As UserActivitySnapshot, uid As String, rawUserId As String, profileMap As Dictionary(Of String, String)) As Task
            Dim s As New UserActivitySection With {
                .Title = "Modération",
                .Columns = New String() {"Date", "Type", "Utilisateur", "Statut / raison", "Message"}
            }

            Dim reports = Await TryArrayAsync($"message_reports?or=(reporter_id.eq.{uid},reported_user_id.eq.{uid})&select=*&order=created_at.desc&limit=300", data.Errors, "signalements")
            For Each o As JObject In reports
                Dim isReporter = Value(o, "reporter_id") = rawUserId
                Dim otherId = If(isReporter, Value(o, "reported_user_id"), Value(o, "reporter_id"))
                s.Rows.Add(New String() {DateValue(o, "created_at"), If(isReporter, "Signalement envoyé", "Signalé"), Lookup(profileMap, otherId), Value(o, "status") & " / " & Value(o, "reason"), ShortText(Value(o, "message_content_snapshot"))})
            Next

            Dim warnings = Await TryArrayAsync($"moderation_warnings?user_id=eq.{uid}&select=*&order=created_at.desc&limit=300", data.Errors, "avertissements")
            For Each o As JObject In warnings
                s.Rows.Add(New String() {DateValue(o, "created_at"), "Avertissement", Lookup(profileMap, Value(o, "created_by")), Value(o, "reason"), ShortText(Value(o, "message"))})
            Next

            data.Sections.Add(s)
        End Function

        Private Async Function LoadMapAsync(query As String, keyName As String, display As Func(Of JObject, String), errors As List(Of String), label As String) As Task(Of Dictionary(Of String, String))
            Dim map As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Dim arr = Await TryArrayAsync(query, errors, label)
            For Each o As JObject In arr
                Dim key = Value(o, keyName, "")
                If Not String.IsNullOrWhiteSpace(key) AndAlso Not map.ContainsKey(key) Then map(key) = display(o)
            Next
            Return map
        End Function

        Private Async Function TryArrayAsync(query As String, errors As List(Of String), sectionName As String) As Task(Of JArray)
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(query)
                Return JArray.Parse(raw)
            Catch ex As Exception
                errors.Add(sectionName & ": " & ex.Message)
                Return New JArray()
            End Try
        End Function

        Private Function DisplayUser(o As JObject) As String
            Dim displayName = Value(o, "display_name", "")
            If Not String.IsNullOrWhiteSpace(displayName) Then Return displayName
            Return Value(o, "username")
        End Function

        Private Function Lookup(map As Dictionary(Of String, String), id As String) As String
            If String.IsNullOrWhiteSpace(id) OrElse id = "—" Then Return "—"
            If map IsNot Nothing AndAlso map.ContainsKey(id) Then Return map(id)
            Return id
        End Function

        Private Function Value(o As JObject, key As String, Optional fallback As String = "—") As String
            If o Is Nothing OrElse o(key) Is Nothing OrElse o(key).Type = JTokenType.Null Then Return fallback
            Dim s = o(key).ToString()
            If String.IsNullOrWhiteSpace(s) Then Return fallback
            Return s
        End Function

        Private Function BoolValue(o As JObject, key As String) As Boolean
            If o Is Nothing OrElse o(key) Is Nothing OrElse o(key).Type = JTokenType.Null Then Return False
            Dim b As Boolean
            If Boolean.TryParse(o(key).ToString(), b) Then Return b
            Return False
        End Function

        Private Function DateValue(o As JObject, key As String) As String
            If o Is Nothing OrElse o(key) Is Nothing OrElse o(key).Type = JTokenType.Null Then Return "—"
            Dim d As DateTime
            If DateTime.TryParse(o(key).ToString(), d) Then Return DateValue(d)
            Return o(key).ToString()
        End Function

        Private Function DateValue(d As DateTime) As String
            If d = DateTime.MinValue Then Return "—"
            Return d.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        End Function

        Private Function YesNo(value As Boolean) As String
            Return If(value, "Oui", "Non")
        End Function

        Private Function Safe(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return "—"
            Return value
        End Function

        Private Function ShortText(value As String) As String
            If String.IsNullOrWhiteSpace(value) OrElse value = "—" Then Return "—"
            Dim clean = value.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
            If clean.Length <= 160 Then Return clean
            Return clean.Substring(0, 157) & "..."
        End Function

    End Module

End Namespace
