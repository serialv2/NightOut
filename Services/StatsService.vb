Option Strict Off
Option Explicit On

Imports System.Linq
Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Models

Namespace Services

    ''' <summary>
    ''' Agrégation des statistiques globales pour le tableau de bord.
    ''' Utilise les comptes exacts PostgREST (rapides) + quelques agrégations
    ''' calculées côté client.
    ''' </summary>
    Public Module StatsService

        Public Async Function GetDashboardAsync() As Task(Of DashboardStats)
            Dim s As New DashboardStats()

            ' ── Compteurs (rapides via Content-Range) ──
            s.BarsTotal = Await SupabaseClient.CountAsync("bars")
            s.BarsApproved = Await SupabaseClient.CountAsync("bars", "status=eq.approved")
            s.BarsPending = Await SupabaseClient.CountAsync("bars", "status=eq.pending")
            s.BarsRejected = Await SupabaseClient.CountAsync("bars", "status=eq.rejected")

            s.EventsTotal = Await SupabaseClient.CountAsync("official_events")
            s.ProTotal = Await SupabaseClient.CountAsync("professional_accounts")
            s.ProPending = Await SupabaseClient.CountAsync("professional_accounts", "status=eq.pending")
            s.UsersTotal = Await SupabaseClient.CountAsync("profiles")
            s.FollowersTotal = Await SupabaseClient.CountAsync("bar_followers")
            Await LoadGlobalGenderStatsAsync(s)

            ' checkins peut ne pas exister selon l'état du schéma → tolérant
            Try
                s.CheckinsTotal = Await SupabaseClient.CountAsync("checkins")
                Await LoadCheckinGenderStatsAsync(s)
            Catch
                s.CheckinsTotal = 0
            End Try

            Try
                s.BarProfileViewsTotal = Await SupabaseClient.CountAsync("bar_profile_views")
            Catch
                s.BarProfileViewsTotal = 0
            End Try

            ' ── Événements en cours / à venir (calcul client) ──
            Try
                Dim events = Await SupabaseClient.GetListAsync(Of OfficialEvent)(
                    "official_events?select=*&status=eq.published")
                Dim now = DateTime.UtcNow
                s.EventsLive = events.Where(Function(e) e.IsLiveNow).Count()
                s.EventsUpcoming = events.Where(Function(e) e.StartAt.ToUniversalTime() > now).Count()
            Catch
            End Try

            ' ── Top établissements par abonnés ──
            Try
                s.TopBars = Await GetTopBarsByFollowersAsync(6)
            Catch
            End Try

            ' ── Répartition des bars par catégorie ──
            Try
                s.BarsByCategory = Await GetBarsByCategoryAsync()
            Catch
            End Try

            Return s
        End Function

        Private Async Function LoadGlobalGenderStatsAsync(s As DashboardStats) As Task
            Try
                Dim raw = Await SupabaseClient.GetRawAsync("profiles?select=id,gender&limit=10000")
                For Each p In JArray.Parse(raw)
                    AddGenderCount(p("gender")?.ToString(), s.UsersFemale, s.UsersMale, s.UsersGenderUnknown)
                Next
            Catch
            End Try
        End Function

        Private Async Function LoadCheckinGenderStatsAsync(s As DashboardStats) As Task
            Dim profileGenderById As New Dictionary(Of String, String)()

            Try
                Dim rawProfiles = Await SupabaseClient.GetRawAsync("profiles?select=id,gender&limit=10000")
                For Each p In JArray.Parse(rawProfiles)
                    Dim id = p("id")?.ToString()
                    If Not String.IsNullOrWhiteSpace(id) Then
                        profileGenderById(id) = p("gender")?.ToString()
                    End If
                Next

                Dim rawCheckins = Await SupabaseClient.GetRawAsync("checkins?select=user_id,gender_snapshot&limit=10000")
                For Each c In JArray.Parse(rawCheckins)
                    Dim gender As String = c("gender_snapshot")?.ToString()
                    If String.IsNullOrWhiteSpace(gender) Then
                        Dim userId = c("user_id")?.ToString()
                        If Not String.IsNullOrWhiteSpace(userId) AndAlso profileGenderById.ContainsKey(userId) Then
                            gender = profileGenderById(userId)
                        End If
                    End If

                    AddGenderCount(gender, s.CheckinsFemale, s.CheckinsMale, s.CheckinsGenderUnknown)
                Next
            Catch
            End Try
        End Function

        Private Sub AddGenderCount(gender As String, ByRef female As Integer, ByRef male As Integer, ByRef unknown As Integer)
            Select Case If(gender, String.Empty).Trim().ToLowerInvariant()
                Case "femme", "female", "woman"
                    female += 1
                Case "homme", "male", "man"
                    male += 1
                Case Else
                    unknown += 1
            End Select
        End Sub

        Private Async Function GetTopBarsByFollowersAsync(top As Integer) _
            As Task(Of List(Of KeyValuePair(Of String, Integer)))

            Dim result As New List(Of KeyValuePair(Of String, Integer))()

            ' On récupère tous les liens bar_followers et on compte par bar_id
            Dim raw = Await SupabaseClient.GetRawAsync("bar_followers?select=bar_id")
            Dim arr = JArray.Parse(raw)
            Dim counts As New Dictionary(Of String, Integer)()
            For Each item In arr
                Dim bid = If(item("bar_id") IsNot Nothing, item("bar_id").ToString(), Nothing)
                If String.IsNullOrEmpty(bid) Then Continue For
                If counts.ContainsKey(bid) Then
                    counts(bid) += 1
                Else
                    counts(bid) = 1
                End If
            Next

            If counts.Count = 0 Then Return result

            ' Résolution des noms de bars
            Dim bars = Await BarService.GetAllAsync()
            Dim nameMap = bars.ToDictionary(Function(b) b.Id, Function(b) b.Name)

            Dim ordered = counts.OrderByDescending(Function(kv) kv.Value).Take(top)
            For Each kv In ordered
                Dim nm = If(nameMap.ContainsKey(kv.Key), nameMap(kv.Key), "Bar")
                result.Add(New KeyValuePair(Of String, Integer)(nm, kv.Value))
            Next
            Return result
        End Function

        Private Async Function GetBarsByCategoryAsync() _
            As Task(Of List(Of KeyValuePair(Of String, Integer)))

            Dim result As New List(Of KeyValuePair(Of String, Integer))()
            Dim bars = Await BarService.GetAllAsync()

            Dim counts As New Dictionary(Of String, Integer)()
            For Each b In bars
                Dim cat = b.Category
                If String.IsNullOrWhiteSpace(cat) Then cat = "Sans catégorie"
                ' Prend la 1re catégorie si plusieurs séparées par virgule
                Dim primary = cat.Split(","c)(0).Trim()
                If String.IsNullOrEmpty(primary) Then primary = "Sans catégorie"
                If counts.ContainsKey(primary) Then
                    counts(primary) += 1
                Else
                    counts(primary) = 1
                End If
            Next

            For Each kv In counts.OrderByDescending(Function(x) x.Value)
                result.Add(New KeyValuePair(Of String, Integer)(kv.Key, kv.Value))
            Next
            Return result
        End Function

    End Module

End Namespace
