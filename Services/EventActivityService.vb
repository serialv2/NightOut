Option Strict Off
Option Explicit On

Imports System.Linq
Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Models

Namespace Services

    Public Class EventActivitySection
        Public Property Title As String
        Public Property Columns As String()
        Public Property Rows As New List(Of String())()
    End Class

    Public Class EventActivitySnapshot
        Public Property OfficialEvent As OfficialEvent
        Public Property Sections As New List(Of EventActivitySection)()
        Public Property Errors As New List(Of String)()
    End Class

    Public Module EventActivityService

        Public Async Function GetAsync(row As NightOutAdmin.Forms.EventsForm.AdminEventRow) As Task(Of EventActivitySnapshot)
            If row Is Nothing Then Return New EventActivitySnapshot()
            If row.Kind = "ephemeral" Then Return Await GetAsync(row.Ephemeral)
            Return Await GetAsync(row.Official)
        End Function

        Public Async Function GetAsync(ev As OfficialEvent) As Task(Of EventActivitySnapshot)
            Dim data As New EventActivitySnapshot With {.OfficialEvent = ev}
            If ev Is Nothing OrElse String.IsNullOrWhiteSpace(ev.Id) Then Return data

            Dim eventId = Uri.EscapeDataString(ev.Id)
            Dim profiles = Await LoadProfileMapAsync(data.Errors)
            Dim bars = Await LoadMapAsync("bars?select=id,name,address&limit=1000", "id", Function(o) Value(o, "name"), data.Errors, "bars")

            Dim participants = Await TryArrayAsync($"official_event_participants?official_event_id=eq.{eventId}&select=*&order=created_at.desc&limit=2000", data.Errors, "participants")
            Dim checkins = Await TryArrayAsync($"checkins?event_id=eq.{eventId}&select=*&order=checked_in_at.desc&limit=2000", data.Errors, "check-ins")
            Dim proStats = Await TryArrayAsync($"pro_event_demographic_stats?official_event_id=eq.{eventId}&select=*&limit=1", data.Errors, "stats pro")
            Dim views = Await LoadViewsAsync(eventId)

            data.Sections.Add(BuildSummary(ev, views.Count, views.SourceName))
            data.Sections.Add(BuildCounters(participants, checkins, views))
            data.Sections.Add(BuildDemographics(participants, checkins, profiles, proStats))
            data.Sections.Add(BuildParticipants(participants, profiles))
            data.Sections.Add(BuildCheckins(checkins, profiles, bars))
            data.Sections.Add(BuildViews(views, profiles))

            If data.Errors.Count > 0 Then
                Dim s As New EventActivitySection With {
                    .Title = "Infos techniques",
                    .Columns = New String() {"Section", "Message"}
                }
                For Each message In data.Errors
                    Dim p = message.Split(New String() {": "}, 2, StringSplitOptions.None)
                    s.Rows.Add(New String() {p(0), If(p.Length > 1, p(1), message)})
                Next
                data.Sections.Add(s)
            End If

            Return data
        End Function

        Public Async Function GetAsync(ev As EphemeralEvent) As Task(Of EventActivitySnapshot)
            Dim data As New EventActivitySnapshot()
            If ev Is Nothing OrElse String.IsNullOrWhiteSpace(ev.Id) Then Return data

            Dim eventId = Uri.EscapeDataString(ev.Id)
            Dim profiles = Await LoadProfileMapAsync(data.Errors)
            Dim bars = Await LoadMapAsync("bars?select=id,name,address&limit=1000", "id", Function(o) Value(o, "name"), data.Errors, "bars")

            Dim participants = Await TryArrayAsync($"ephemeral_event_participants?ephemeral_event_id=eq.{eventId}&select=*&order=joined_at.desc&limit=2000", data.Errors, "participants ephemeres")
            Dim checkins = Await TryArrayAsync($"checkins?event_id=eq.{eventId}&select=*&order=checked_in_at.desc&limit=2000", data.Errors, "check-ins")
            Dim views = Await LoadEphemeralViewsAsync(eventId)
            Dim emptyProStats As New JArray()

            data.Sections.Add(BuildEphemeralSummary(ev, views.Count, views.SourceName))
            data.Sections.Add(BuildCounters(participants, checkins, views))
            data.Sections.Add(BuildDemographics(participants, checkins, profiles, emptyProStats))
            data.Sections.Add(BuildParticipants(participants, profiles))
            data.Sections.Add(BuildCheckins(checkins, profiles, bars))
            data.Sections.Add(BuildViews(views, profiles))

            If data.Errors.Count > 0 Then
                Dim s As New EventActivitySection With {
                    .Title = "Infos techniques",
                    .Columns = New String() {"Section", "Message"}
                }
                For Each message In data.Errors
                    Dim p = message.Split(New String() {": "}, 2, StringSplitOptions.None)
                    s.Rows.Add(New String() {p(0), If(p.Length > 1, p(1), message)})
                Next
                data.Sections.Add(s)
            End If

            Return data
        End Function

        Private Function BuildSummary(ev As OfficialEvent, viewsCount As Integer?, viewSource As String) As EventActivitySection
            Dim s As New EventActivitySection With {
                .Title = "Résumé",
                .Columns = New String() {"Champ", "Valeur"}
            }
            s.Rows.Add(New String() {"ID", Safe(ev.Id)})
            s.Rows.Add(New String() {"Titre", Safe(ev.Title)})
            s.Rows.Add(New String() {"Établissement", Safe(ev.BarName)})
            s.Rows.Add(New String() {"Bar ID", Safe(ev.BarId)})
            s.Rows.Add(New String() {"Statut", Safe(ev.StatusLabel)})
            s.Rows.Add(New String() {"Début", DateValue(ev.StartAt)})
            s.Rows.Add(New String() {"Fin", If(ev.EndAt.HasValue, DateValue(ev.EndAt.Value), "—")})
            s.Rows.Add(New String() {"Vues fiche", If(viewsCount.HasValue, viewsCount.Value.ToString(), "Non suivi")})
            s.Rows.Add(New String() {"Source vues", Safe(viewSource)})
            Return s
        End Function

        Private Function BuildEphemeralSummary(ev As EphemeralEvent, viewsCount As Integer?, viewSource As String) As EventActivitySection
            Dim s As New EventActivitySection With {
                .Title = "Résumé",
                .Columns = New String() {"Champ", "Valeur"}
            }
            s.Rows.Add(New String() {"Type", "Éphémère"})
            s.Rows.Add(New String() {"ID", Safe(ev.Id)})
            s.Rows.Add(New String() {"Titre", Safe(ev.Title)})
            s.Rows.Add(New String() {"Créateur", Safe(If(ev.CreatorName, ev.CreatorId))})
            s.Rows.Add(New String() {"Bar", Safe(ev.BarName)})
            s.Rows.Add(New String() {"Bar ID", Safe(ev.BarId)})
            s.Rows.Add(New String() {"Lieu", Safe(ev.PlaceName)})
            s.Rows.Add(New String() {"Adresse", Safe(ev.Address)})
            s.Rows.Add(New String() {"Visibilité", Safe(ev.Visibility)})
            s.Rows.Add(New String() {"Groupe ID", Safe(ev.GroupId)})
            s.Rows.Add(New String() {"Catégorie", Safe(ev.Category)})
            s.Rows.Add(New String() {"Statut", Safe(ev.StatusLabel)})
            s.Rows.Add(New String() {"Début", DateValue(ev.StartAt)})
            s.Rows.Add(New String() {"Fin", DateValue(ev.ExpiresAt)})
            s.Rows.Add(New String() {"Vues fiche", If(viewsCount.HasValue, viewsCount.Value.ToString(), "Non suivi")})
            s.Rows.Add(New String() {"Source vues", Safe(viewSource)})
            Return s
        End Function

        Private Function BuildCounters(participants As JArray, checkins As JArray, views As ViewResult) As EventActivitySection
            Dim going = CountWhere(participants, Function(o) Value(o, "status") = "going")
            Dim maybe = CountWhere(participants, Function(o) Value(o, "status") = "maybe")
            Dim notGoing = CountWhere(participants, Function(o) Value(o, "status") = "not_going")
            Dim checked = CountWhere(participants, Function(o) BoolValue(o, "checked_in"))

            Dim s As New EventActivitySection With {
                .Title = "Compteurs",
                .Columns = New String() {"Statistique", "Valeur"}
            }
            s.Rows.Add(New String() {"Vues fiche", If(views.Count.HasValue, views.Count.Value.ToString(), "Non suivi")})
            s.Rows.Add(New String() {"Participants annoncés", participants.Count.ToString()})
            s.Rows.Add(New String() {"J'y vais", going.ToString()})
            s.Rows.Add(New String() {"Peut-être", maybe.ToString()})
            s.Rows.Add(New String() {"Ne vient pas", notGoing.ToString()})
            s.Rows.Add(New String() {"Check-ins événement", checked.ToString()})
            s.Rows.Add(New String() {"Check-ins bar liés à l'événement", checkins.Count.ToString()})
            Return s
        End Function

        Private Function CountWhere(arr As JArray, predicate As Func(Of JObject, Boolean)) As Integer
            If arr Is Nothing Then Return 0
            Dim total = 0
            For Each token In arr
                Dim o = TryCast(token, JObject)
                If o IsNot Nothing AndAlso predicate(o) Then total += 1
            Next
            Return total
        End Function

        Private Function BuildDemographics(participants As JArray, checkins As JArray, profiles As Dictionary(Of String, ProfileMini), proStats As JArray) As EventActivitySection
            Dim s As New EventActivitySection With {
                .Title = "Genre / âge",
                .Columns = New String() {"Population", "Total", "Femmes", "Hommes", "Autres", "Non renseigné", "18-24", "25-34", "35-44", "45+", "Âge inconnu"}
            }

            Dim announced = CountPeople(participants.Select(Function(o) Value(CType(o, JObject), "user_id")), profiles)
            Dim eventChecked = CountPeople(participants.Where(Function(o) BoolValue(CType(o, JObject), "checked_in")).Select(Function(o) Value(CType(o, JObject), "user_id")), profiles)
            Dim linkedCheckins = CountPeople(checkins.Select(Function(o) Value(CType(o, JObject), "user_id")), profiles, checkins)

            s.Rows.Add(ToDemoRow("Annoncés", announced))
            s.Rows.Add(ToDemoRow("Check-ins événement", eventChecked))
            s.Rows.Add(ToDemoRow("Check-ins bar liés", linkedCheckins))

            If proStats IsNot Nothing AndAlso proStats.Count > 0 Then
                Dim o = CType(proStats(0), JObject)
                s.Rows.Add(New String() {
                    "Vue SQL pro",
                    Value(o, "checked_in_total", "0"),
                    Value(o, "checked_in_female", "0"),
                    Value(o, "checked_in_male", "0"),
                    Value(o, "checked_in_other", "0"),
                    Value(o, "checked_in_gender_unknown", "0"),
                    Value(o, "age_18_24", "0"),
                    Value(o, "age_25_34", "0"),
                    Value(o, "age_35_44", "0"),
                    Value(o, "age_45_plus", "0"),
                    Value(o, "age_unknown", "0")
                })
            End If

            Return s
        End Function

        Private Function BuildParticipants(participants As JArray, profiles As Dictionary(Of String, ProfileMini)) As EventActivitySection
            Dim s As New EventActivitySection With {
                .Title = "Participants",
                .Columns = New String() {"Date", "Utilisateur", "Statut", "Check-in", "Source", "Genre", "Âge"}
            }
            For Each o As JObject In participants
                Dim userId = Value(o, "user_id")
                Dim p = LookupProfile(profiles, userId)
                s.Rows.Add(New String() {
                    DateValue(o, FirstExistingDateKey(o, "created_at", "joined_at", "updated_at")),
                    p.Display,
                    Value(o, "status"),
                    If(BoolValue(o, "checked_in"), DateValue(o, "checked_in_at"), "Non"),
                    Value(o, "source"),
                    Safe(p.Gender),
                    AgeLabel(p.Age)
                })
            Next
            Return s
        End Function

        Private Function BuildCheckins(checkins As JArray, profiles As Dictionary(Of String, ProfileMini), bars As Dictionary(Of String, String)) As EventActivitySection
            Dim s As New EventActivitySection With {
                .Title = "Check-ins",
                .Columns = New String() {"Entrée", "Sortie", "Utilisateur", "Bar", "Genre snapshot", "Âge snapshot", "Tranche"}
            }
            For Each o As JObject In checkins
                Dim p = LookupProfile(profiles, Value(o, "user_id"))
                s.Rows.Add(New String() {
                    DateValue(o, "checked_in_at"),
                    DateValue(o, "checked_out_at"),
                    p.Display,
                    Lookup(bars, Value(o, "bar_id")),
                    Value(o, "gender_snapshot"),
                    Value(o, "age_snapshot"),
                    Value(o, "age_band_snapshot")
                })
            Next
            Return s
        End Function

        Private Function BuildViews(views As ViewResult, profiles As Dictionary(Of String, ProfileMini)) As EventActivitySection
            Dim s As New EventActivitySection With {
                .Title = "Vues",
                .Columns = New String() {"Date", "Utilisateur", "Source", "Détail"}
            }

            If Not views.Count.HasValue Then
                s.Rows.Add(New String() {"—", "Non suivi", "—", "Aucune table de vues événement détectée pour l'instant."})
                Return s
            End If

            For Each o As JObject In views.Rows
                Dim p = LookupProfile(profiles, FirstValue(o, "user_id", "viewer_id", "profile_id"))
                s.Rows.Add(New String() {
                    DateValue(o, FirstExistingDateKey(o, "created_at", "viewed_at", "seen_at")),
                    p.Display,
                    views.SourceName,
                    Safe(FirstValue(o, "source", "screen", "referrer"))
                })
            Next
            Return s
        End Function

        Private Async Function LoadViewsAsync(eventId As String) As Task(Of ViewResult)
            Dim candidates = New List(Of Tuple(Of String, String)) From {
                Tuple.Create("official_event_views", $"official_event_views?official_event_id=eq.{eventId}&select=*&order=created_at.desc&limit=2000"),
                Tuple.Create("event_views", $"event_views?event_id=eq.{eventId}&select=*&order=created_at.desc&limit=2000"),
                Tuple.Create("event_profile_views", $"event_profile_views?event_id=eq.{eventId}&select=*&order=created_at.desc&limit=2000")
            }

            For Each candidate In candidates
                Try
                    Dim raw = Await SupabaseClient.GetRawAsync(candidate.Item2)
                    Return New ViewResult With {.SourceName = candidate.Item1, .Rows = JArray.Parse(raw), .Count = JArray.Parse(raw).Count}
                Catch
                End Try
            Next

            Return New ViewResult With {.SourceName = "Non suivi", .Rows = New JArray(), .Count = Nothing}
        End Function

        Private Async Function LoadEphemeralViewsAsync(eventId As String) As Task(Of ViewResult)
            Dim candidates = New List(Of Tuple(Of String, String)) From {
                Tuple.Create("ephemeral_event_views", $"ephemeral_event_views?ephemeral_event_id=eq.{eventId}&select=*&order=created_at.desc&limit=2000"),
                Tuple.Create("event_views", $"event_views?event_id=eq.{eventId}&select=*&order=created_at.desc&limit=2000"),
                Tuple.Create("event_profile_views", $"event_profile_views?event_id=eq.{eventId}&select=*&order=created_at.desc&limit=2000")
            }

            For Each candidate In candidates
                Try
                    Dim raw = Await SupabaseClient.GetRawAsync(candidate.Item2)
                    Dim rows = JArray.Parse(raw)
                    Return New ViewResult With {.SourceName = candidate.Item1, .Rows = rows, .Count = rows.Count}
                Catch
                End Try
            Next

            Return New ViewResult With {.SourceName = "Non suivi", .Rows = New JArray(), .Count = Nothing}
        End Function

        Private Class ViewResult
            Public Property SourceName As String
            Public Property Rows As JArray
            Public Property Count As Integer?
        End Class

        Private Class DemoCount
            Public Property Total As Integer
            Public Property Female As Integer
            Public Property Male As Integer
            Public Property Other As Integer
            Public Property UnknownGender As Integer
            Public Property Age18To24 As Integer
            Public Property Age25To34 As Integer
            Public Property Age35To44 As Integer
            Public Property Age45Plus As Integer
            Public Property AgeUnknown As Integer
        End Class

        Private Function CountPeople(userIds As IEnumerable(Of String), profiles As Dictionary(Of String, ProfileMini), Optional checkins As JArray = Nothing) As DemoCount
            Dim d As New DemoCount()
            Dim index As Integer = 0

            For Each userId In userIds
                Dim p = LookupProfile(profiles, userId)
                Dim gender = p.Gender
                Dim age = p.Age

                If checkins IsNot Nothing AndAlso index < checkins.Count Then
                    Dim c = CType(checkins(index), JObject)
                    gender = Value(c, "gender_snapshot", gender)
                    Dim snapAge As Integer
                    If Integer.TryParse(Value(c, "age_snapshot", ""), snapAge) Then age = snapAge
                End If

                d.Total += 1
                AddGender(gender, d)
                AddAge(age, d)
                index += 1
            Next

            Return d
        End Function

        Private Function ToDemoRow(label As String, d As DemoCount) As String()
            Return New String() {
                label,
                d.Total.ToString(),
                d.Female.ToString(),
                d.Male.ToString(),
                d.Other.ToString(),
                d.UnknownGender.ToString(),
                d.Age18To24.ToString(),
                d.Age25To34.ToString(),
                d.Age35To44.ToString(),
                d.Age45Plus.ToString(),
                d.AgeUnknown.ToString()
            }
        End Function

        Private Sub AddGender(gender As String, d As DemoCount)
            Select Case If(gender, String.Empty).Trim().ToLowerInvariant()
                Case "femme", "female", "woman"
                    d.Female += 1
                Case "homme", "male", "man"
                    d.Male += 1
                Case "other", "autre", "non_binary", "non-binary"
                    d.Other += 1
                Case Else
                    d.UnknownGender += 1
            End Select
        End Sub

        Private Sub AddAge(age As Integer?, d As DemoCount)
            If Not age.HasValue OrElse age.Value <= 0 Then
                d.AgeUnknown += 1
            ElseIf age.Value <= 24 Then
                d.Age18To24 += 1
            ElseIf age.Value <= 34 Then
                d.Age25To34 += 1
            ElseIf age.Value <= 44 Then
                d.Age35To44 += 1
            Else
                d.Age45Plus += 1
            End If
        End Sub

        Private Class ProfileMini
            Public Property Display As String
            Public Property Gender As String
            Public Property Age As Integer?
        End Class

        Private Async Function LoadProfileMapAsync(errors As List(Of String)) As Task(Of Dictionary(Of String, ProfileMini))
            Dim map As New Dictionary(Of String, ProfileMini)(StringComparer.OrdinalIgnoreCase)
            Dim arr = Await TryArrayAsync("profiles?select=id,username,display_name,gender,birthdate&limit=10000", errors, "profils")
            For Each o As JObject In arr
                Dim id = Value(o, "id", "")
                If String.IsNullOrWhiteSpace(id) Then Continue For
                map(id) = New ProfileMini With {
                    .Display = DisplayUser(o),
                    .Gender = Value(o, "gender", ""),
                    .Age = AgeFromBirthdate(Value(o, "birthdate", ""))
                }
            Next
            Return map
        End Function

        Private Function LookupProfile(map As Dictionary(Of String, ProfileMini), id As String) As ProfileMini
            If Not String.IsNullOrWhiteSpace(id) AndAlso map IsNot Nothing AndAlso map.ContainsKey(id) Then Return map(id)
            Return New ProfileMini With {.Display = Safe(id), .Gender = "", .Age = Nothing}
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

        Private Function FirstValue(o As JObject, ParamArray keys As String()) As String
            For Each key In keys
                Dim v = Value(o, key, "")
                If Not String.IsNullOrWhiteSpace(v) Then Return v
            Next
            Return "—"
        End Function

        Private Function FirstExistingDateKey(o As JObject, ParamArray keys As String()) As String
            For Each key In keys
                If o IsNot Nothing AndAlso o(key) IsNot Nothing AndAlso o(key).Type <> JTokenType.Null Then Return key
            Next
            Return keys(0)
        End Function

        Private Function Value(o As JObject, key As String, Optional fallback As String = "—") As String
            If o Is Nothing OrElse String.IsNullOrWhiteSpace(key) OrElse o(key) Is Nothing OrElse o(key).Type = JTokenType.Null Then Return fallback
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
            If o Is Nothing OrElse String.IsNullOrWhiteSpace(key) OrElse o(key) Is Nothing OrElse o(key).Type = JTokenType.Null Then Return "—"
            Dim d As DateTime
            If DateTime.TryParse(o(key).ToString(), d) Then Return DateValue(d)
            Return o(key).ToString()
        End Function

        Private Function DateValue(d As DateTime) As String
            If d = DateTime.MinValue Then Return "—"
            Return d.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        End Function

        Private Function AgeFromBirthdate(value As String) As Integer?
            Dim birth As DateTime
            If Not DateTime.TryParse(value, birth) Then Return Nothing
            Dim today = Date.Today
            Dim age = today.Year - birth.Year
            If birth.Date > today.AddYears(-age) Then age -= 1
            If age < 0 OrElse age > 120 Then Return Nothing
            Return age
        End Function

        Private Function AgeLabel(age As Integer?) As String
            If Not age.HasValue Then Return "—"
            Return age.Value.ToString()
        End Function

        Private Function Safe(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return "—"
            Return value
        End Function

    End Module

End Namespace
