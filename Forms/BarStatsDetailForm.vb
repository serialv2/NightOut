Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    ''' <summary>Vue admin détaillée d'un bar : stats, audience et événements.</summary>
    Public Class BarStatsDetailForm
        Inherits Form

        Private ReadOnly _bar As Bar
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly lblSubtitle As New Label()
        Private ReadOnly flowCards As New FlowLayoutPanel()
        Private ReadOnly gridInfo As New DataGridView()
        Private ReadOnly gridHours As New DataGridView()
        Private ReadOnly gridEvents As New DataGridView()
        Private ReadOnly gridCheckins As New DataGridView()
        Private ReadOnly gridViews As New DataGridView()
        Private ReadOnly gridFollowers As New DataGridView()
        Private ReadOnly btnEdit As New Button()
        Private ReadOnly btnClose As New Button()

        Public Sub New(bar As Bar)
            _bar = bar

            Me.Text = "Stats bar — " & If(bar.Name, "")
            Me.BackColor = NightOutTheme.BgDark
            Me.FormBorderStyle = FormBorderStyle.Sizable
            Me.StartPosition = FormStartPosition.CenterParent
            Me.ClientSize = New Size(980, 720)
            Me.MinimumSize = New Size(820, 560)

            Dim header As New Panel() With {.Dock = DockStyle.Top, .Height = 96, .BackColor = NightOutTheme.BgPanel, .Padding = New Padding(18, 12, 18, 10)}

            lblTitle.Text = If(bar.Name, "(sans nom)")
            lblTitle.ForeColor = NightOutTheme.Gold
            lblTitle.Font = NightOutTheme.FontTitle(18.0F)
            lblTitle.Dock = DockStyle.Top
            lblTitle.Height = 34

            lblSubtitle.Text = $"{If(bar.Address, "")} · {StatusLabel(bar.Status)}"
            lblSubtitle.ForeColor = NightOutTheme.Muted
            lblSubtitle.Font = NightOutTheme.FontBody(10.0F)
            lblSubtitle.Dock = DockStyle.Top
            lblSubtitle.Height = 24

            Dim actions As New FlowLayoutPanel() With {
                .Dock = DockStyle.Right,
                .Width = 280,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .BackColor = NightOutTheme.BgPanel,
                .Padding = New Padding(0, 22, 0, 0)
            }

            NightOutTheme.StylePrimaryButton(btnEdit, NightOutTheme.Gold)
            btnEdit.Text = "Modifier la fiche"
            btnEdit.Width = 150

            NightOutTheme.StyleGhostButton(btnClose, NightOutTheme.Muted)
            btnClose.Text = "Fermer"
            btnClose.Width = 90
            btnClose.DialogResult = DialogResult.Cancel

            actions.Controls.Add(btnEdit)
            actions.Controls.Add(btnClose)

            header.Controls.Add(actions)
            header.Controls.Add(lblSubtitle)
            header.Controls.Add(lblTitle)

            Dim scroll As New Panel() With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = NightOutTheme.BgDark, .Padding = New Padding(16)}

            flowCards.Location = New Point(16, 14)
            flowCards.Size = New Size(920, 430)
            flowCards.AutoSize = False
            flowCards.FlowDirection = FlowDirection.LeftToRight
            flowCards.WrapContents = True
            flowCards.BackColor = NightOutTheme.BgDark

            Dim lblInfo As New Label() With {
                .Text = "Infos et propriétaire",
                .Location = New Point(16, 458),
                .Size = New Size(920, 28),
                .ForeColor = NightOutTheme.Gold,
                .Font = NightOutTheme.FontTitle(12.0F)
            }

            gridInfo.Location = New Point(16, 492)
            gridInfo.Size = New Size(920, 220)
            NightOutTheme.StyleGrid(gridInfo)
            gridInfo.AutoGenerateColumns = True

            Dim lblHours As New Label() With {
                .Text = "Horaires",
                .Location = New Point(16, 728),
                .Size = New Size(920, 28),
                .ForeColor = NightOutTheme.Gold,
                .Font = NightOutTheme.FontTitle(12.0F)
            }

            gridHours.Location = New Point(16, 762)
            gridHours.Size = New Size(920, 220)
            NightOutTheme.StyleGrid(gridHours)
            gridHours.AutoGenerateColumns = True

            Dim lblEvents As New Label() With {
                .Text = "Événements du bar",
                .Location = New Point(16, 998),
                .Size = New Size(920, 28),
                .ForeColor = NightOutTheme.Gold,
                .Font = NightOutTheme.FontTitle(12.0F)
            }

            gridEvents.Location = New Point(16, 1032)
            gridEvents.Size = New Size(920, 320)
            NightOutTheme.StyleGrid(gridEvents)
            gridEvents.AutoGenerateColumns = True

            Dim lblCheckins As New Label() With {
                .Text = "Derniers check-ins",
                .Location = New Point(16, 1368),
                .Size = New Size(920, 28),
                .ForeColor = NightOutTheme.Gold,
                .Font = NightOutTheme.FontTitle(12.0F)
            }

            gridCheckins.Location = New Point(16, 1402)
            gridCheckins.Size = New Size(920, 320)
            NightOutTheme.StyleGrid(gridCheckins)
            gridCheckins.AutoGenerateColumns = True

            Dim lblViews As New Label() With {
                .Text = "Dernières vues de fiche",
                .Location = New Point(16, 1738),
                .Size = New Size(920, 28),
                .ForeColor = NightOutTheme.Gold,
                .Font = NightOutTheme.FontTitle(12.0F)
            }

            gridViews.Location = New Point(16, 1772)
            gridViews.Size = New Size(920, 320)
            NightOutTheme.StyleGrid(gridViews)
            gridViews.AutoGenerateColumns = True

            Dim lblFollowers As New Label() With {
                .Text = "Abonnés",
                .Location = New Point(16, 2108),
                .Size = New Size(920, 28),
                .ForeColor = NightOutTheme.Gold,
                .Font = NightOutTheme.FontTitle(12.0F)
            }

            gridFollowers.Location = New Point(16, 2142)
            gridFollowers.Size = New Size(920, 300)
            NightOutTheme.StyleGrid(gridFollowers)
            gridFollowers.AutoGenerateColumns = True

            scroll.Controls.Add(flowCards)
            scroll.Controls.Add(lblInfo)
            scroll.Controls.Add(gridInfo)
            scroll.Controls.Add(lblHours)
            scroll.Controls.Add(gridHours)
            scroll.Controls.Add(lblEvents)
            scroll.Controls.Add(gridEvents)
            scroll.Controls.Add(lblCheckins)
            scroll.Controls.Add(gridCheckins)
            scroll.Controls.Add(lblViews)
            scroll.Controls.Add(gridViews)
            scroll.Controls.Add(lblFollowers)
            scroll.Controls.Add(gridFollowers)

            Me.Controls.Add(scroll)
            Me.Controls.Add(header)

            AddHandler btnEdit.Click, AddressOf Edit_Click
            AddHandler Me.Load, Async Sub() Await LoadStatsAsync()
        End Sub

        Private Async Sub Edit_Click(sender As Object, e As EventArgs)
            Using f As New BarEditForm(_bar)
                If f.ShowDialog(Me) = DialogResult.OK Then
                    DialogResult = DialogResult.OK
                    Await LoadStatsAsync()
                End If
            End Using
        End Sub

        Private Async Function LoadStatsAsync() As Task
            Try
                UseWaitCursor = True
                flowCards.Controls.Clear()
                gridInfo.DataSource = Nothing
                gridHours.DataSource = Nothing
                gridEvents.DataSource = Nothing
                gridCheckins.DataSource = Nothing
                gridViews.DataSource = Nothing
                gridFollowers.DataSource = Nothing

                Dim stats = Await BuildStatsAsync()

                flowCards.Controls.Add(MakeCard("Vues fiche", stats.ProfileViewsTotal, NightOutTheme.Cream))
                flowCards.Controls.Add(MakeCard("Vues filles", stats.ProfileViewsFemale, NightOutTheme.Pink))
                flowCards.Controls.Add(MakeCard("Vues garçons", stats.ProfileViewsMale, NightOutTheme.Blue))
                flowCards.Controls.Add(MakeCard("Vues non renseignées", stats.ProfileViewsUnknown, NightOutTheme.Muted))
                flowCards.Controls.Add(MakeCard("Entrées / check-ins", stats.CheckinsTotal, NightOutTheme.Green))
                flowCards.Controls.Add(MakeCard("Check-ins filles", stats.CheckinsFemale, NightOutTheme.Pink))
                flowCards.Controls.Add(MakeCard("Check-ins garçons", stats.CheckinsMale, NightOutTheme.Blue))
                flowCards.Controls.Add(MakeCard("Check-ins non renseignés", stats.CheckinsUnknown, NightOutTheme.Muted))
                flowCards.Controls.Add(MakeCard("18-24 ans", stats.CheckinsAge18To24, NightOutTheme.Green))
                flowCards.Controls.Add(MakeCard("25-34 ans", stats.CheckinsAge25To34, NightOutTheme.Gold))
                flowCards.Controls.Add(MakeCard("35-44 ans", stats.CheckinsAge35To44, NightOutTheme.Orange))
                flowCards.Controls.Add(MakeCard("45 ans et +", stats.CheckinsAge45Plus, NightOutTheme.Blue))
                flowCards.Controls.Add(MakeCard("Âge non renseigné", stats.CheckinsAgeUnknown, NightOutTheme.Muted))
                flowCards.Controls.Add(MakeCard("Présents maintenant", stats.ActiveCheckins, NightOutTheme.Orange))
                flowCards.Controls.Add(MakeCard("Abonnés", stats.Followers, NightOutTheme.Gold))
                flowCards.Controls.Add(MakeCard("Événements officiels", stats.OfficialEvents, NightOutTheme.Pink))
                flowCards.Controls.Add(MakeCard("Sorties éphémères", stats.EphemeralEvents, NightOutTheme.Blue))

                gridInfo.DataSource = stats.InfoRows
                gridHours.DataSource = stats.Hours
                gridEvents.DataSource = stats.Events
                gridCheckins.DataSource = stats.Checkins
                gridViews.DataSource = stats.Views
                gridFollowers.DataSource = stats.FollowersRows
            Catch ex As Exception
                MessageBox.Show("Impossible de charger les stats du bar : " & ex.Message, "Stats bar",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                UseWaitCursor = False
            End Try
        End Function

        Private Async Function BuildStatsAsync() As Task(Of BarStats)
            Dim s As New BarStats()
            Dim barFilter = "bar_id=eq." & Uri.EscapeDataString(_bar.Id)

            s.ProfileViewsTotal = Await CountSafeAsync("bar_profile_views", barFilter)
            s.CheckinsTotal = Await CountSafeAsync("checkins", barFilter)
            s.ActiveCheckins = Await CountSafeAsync("checkins", barFilter & "&is_active=eq.true")
            s.Followers = Await CountSafeAsync("bar_followers", barFilter)
            s.OfficialEvents = Await CountSafeAsync("official_events", barFilter)
            s.EphemeralEvents = Await CountSafeAsync("ephemeral_events", barFilter)

            Dim profiles = Await LoadProfileDemographicsAsync()
            Await LoadInfoAsync(s, profiles)
            Await LoadHoursAsync(s)
            Dim viewGenderStats = Await LoadGenderCountsAsync("bar_profile_views", "viewer_id", profiles)
            s.ProfileViewsFemale = viewGenderStats.Female
            s.ProfileViewsMale = viewGenderStats.Male
            s.ProfileViewsUnknown = viewGenderStats.Unknown

            Dim checkinGenderStats = Await LoadCheckinGenderCountsAsync(profiles)
            s.CheckinsFemale = checkinGenderStats.Female
            s.CheckinsMale = checkinGenderStats.Male
            s.CheckinsUnknown = checkinGenderStats.Unknown

            Dim checkinAgeStats = Await LoadCheckinAgeCountsAsync(profiles)
            s.CheckinsAge18To24 = checkinAgeStats.Age18To24
            s.CheckinsAge25To34 = checkinAgeStats.Age25To34
            s.CheckinsAge35To44 = checkinAgeStats.Age35To44
            s.CheckinsAge45Plus = checkinAgeStats.Age45Plus
            s.CheckinsAgeUnknown = checkinAgeStats.Unknown
            Await LoadEventsAsync(s)
            Await LoadCheckinRowsAsync(s, profiles)
            Await LoadViewRowsAsync(s, profiles)
            Await LoadFollowerRowsAsync(s, profiles)

            Return s
        End Function

        Private Async Function LoadInfoAsync(s As BarStats, profiles As Dictionary(Of String, ProfileDemographic)) As Task
            Dim owner = LookupProfile(profiles, _bar.OwnerId)
            Dim proName As String = "—"
            Dim proUser As String = "—"
            Try
                If Not String.IsNullOrWhiteSpace(_bar.ProfessionalAccountId) Then
                    Dim raw = Await SupabaseClient.GetRawAsync($"professional_accounts?id=eq.{Uri.EscapeDataString(_bar.ProfessionalAccountId)}&select=*&limit=1")
                    Dim arr = JArray.Parse(raw)
                    If arr.Count > 0 Then
                        Dim p = CType(arr(0), JObject)
                        proName = FirstText(p, "display_name", "legal_name", "id")
                        proUser = LookupProfile(profiles, TextValue(p, "user_id")).Display
                    End If
                End If
            Catch
            End Try

            s.InfoRows.Add(New InfoRow With {.Champ = "ID", .Valeur = If(_bar.Id, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Nom", .Valeur = If(_bar.Name, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Adresse", .Valeur = If(_bar.Address, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Ville", .Valeur = If(_bar.AddressCityName, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Catégorie", .Valeur = If(_bar.Category, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Description", .Valeur = If(_bar.Description, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Téléphone", .Valeur = If(_bar.Phone, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Site web", .Valeur = If(_bar.Website, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Instagram", .Valeur = If(_bar.Instagram, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Coordonnées", .Valeur = $"{_bar.Latitude}, {_bar.Longitude}"})
            s.InfoRows.Add(New InfoRow With {.Champ = "Statut", .Valeur = StatusLabel(_bar.Status)})
            s.InfoRows.Add(New InfoRow With {.Champ = "Actif", .Valeur = If(_bar.IsActive, "Oui", "Non")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Vérifié", .Valeur = If(_bar.IsVerified, "Oui", "Non")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Premium", .Valeur = If(_bar.IsPremium, "Oui", "Non")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Présents compteur bar", .Valeur = _bar.TotalPresent.ToString()})
            s.InfoRows.Add(New InfoRow With {.Champ = "Créé le", .Valeur = DateLabel(_bar.CreatedAt)})
            s.InfoRows.Add(New InfoRow With {.Champ = "Owner", .Valeur = owner.Display})
            s.InfoRows.Add(New InfoRow With {.Champ = "Compte pro", .Valeur = proName})
            s.InfoRows.Add(New InfoRow With {.Champ = "Utilisateur compte pro", .Valeur = proUser})
            s.InfoRows.Add(New InfoRow With {.Champ = "Logo", .Valeur = If(_bar.LogoUrl, "—")})
            s.InfoRows.Add(New InfoRow With {.Champ = "Couverture", .Valeur = If(_bar.CoverUrl, "—")})
        End Function

        Private Async Function LoadHoursAsync(s As BarStats) As Task
            Try
                Dim hours = Await HoursService.GetForBarAsync(_bar.Id)
                For Each h In hours
                    s.Hours.Add(New HourRow With {
                        .Jour = BarOpeningHour.DayNames(Math.Max(0, Math.Min(6, h.DayOfWeek - 1))),
                        .Statut = If(h.IsClosed, "Fermé", "Ouvert"),
                        .Ouverture = If(h.IsClosed, "—", If(h.OpenTime, "—")),
                        .Fermeture = If(h.IsClosed, "—", If(h.CloseTime, "—"))
                    })
                Next
            Catch
            End Try
        End Function

        Private Async Function CountSafeAsync(table As String, filter As String) As Task(Of Integer)
            Try
                Return Await SupabaseClient.CountAsync(table, filter)
            Catch
                Return 0
            End Try
        End Function

        Private Async Function LoadProfileDemographicsAsync() As Task(Of Dictionary(Of String, ProfileDemographic))
            Dim result As New Dictionary(Of String, ProfileDemographic)()
            Try
                Dim raw = Await SupabaseClient.GetRawAsync("profiles?select=id,username,display_name,gender,birthdate&limit=10000")
                For Each p In JArray.Parse(raw)
                    Dim id = p("id")?.ToString()
                    If Not String.IsNullOrWhiteSpace(id) Then
                        Dim birthdate As DateTime?
                        Dim birthRaw = p("birthdate")?.ToString()
                        Dim parsed As DateTime
                        If Not String.IsNullOrWhiteSpace(birthRaw) AndAlso DateTime.TryParse(birthRaw, parsed) Then
                            birthdate = parsed
                        End If

                        result(id) = New ProfileDemographic With {
                            .Display = If(Not String.IsNullOrWhiteSpace(p("display_name")?.ToString()), p("display_name")?.ToString(), If(p("username")?.ToString(), id)),
                            .Gender = p("gender")?.ToString(),
                            .Birthdate = birthdate
                        }
                    End If
                Next
            Catch
            End Try
            Return result
        End Function

        Private Async Function LoadGenderCountsAsync(table As String,
                                                     userColumn As String,
                                                     profiles As Dictionary(Of String, ProfileDemographic)) As Task(Of GenderStats)
            Dim result As New GenderStats()
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(
                    $"{table}?select={userColumn}&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&limit=10000")
                For Each row In JArray.Parse(raw)
                    Dim userId = row(userColumn)?.ToString()
                    Dim gender As String = Nothing
                    If Not String.IsNullOrWhiteSpace(userId) AndAlso profiles.ContainsKey(userId) Then
                        gender = profiles(userId).Gender
                    End If
                    AddGender(gender, result)
                Next
            Catch
            End Try
            Return result
        End Function

        Private Async Function LoadCheckinGenderCountsAsync(profiles As Dictionary(Of String, ProfileDemographic)) As Task(Of GenderStats)
            Dim result As New GenderStats()
            Dim useFallback = False
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(
                    $"checkins?select=user_id,gender_snapshot&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&limit=10000")
                For Each row In JArray.Parse(raw)
                    Dim gender = row("gender_snapshot")?.ToString()
                    If String.IsNullOrWhiteSpace(gender) Then
                        Dim userId = row("user_id")?.ToString()
                        If Not String.IsNullOrWhiteSpace(userId) AndAlso profiles.ContainsKey(userId) Then
                            gender = profiles(userId).Gender
                        End If
                    End If
                    AddGender(gender, result)
                Next
            Catch
                useFallback = True
            End Try
            If useFallback Then Return Await LoadGenderCountsAsync("checkins", "user_id", profiles)
            Return result
        End Function

        Private Async Function LoadCheckinAgeCountsAsync(profiles As Dictionary(Of String, ProfileDemographic)) As Task(Of AgeStats)
            Dim result As New AgeStats()
            Dim useFallback = False
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(
                    $"checkins?select=user_id,age_snapshot,age_band_snapshot&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&limit=10000")
                For Each row In JArray.Parse(raw)
                    Dim band = row("age_band_snapshot")?.ToString()
                    If Not String.IsNullOrWhiteSpace(band) Then
                        AddAgeBand(band, result)
                        Continue For
                    End If

                    Dim ageValue As Integer
                    Dim ageRaw = row("age_snapshot")?.ToString()
                    If Not String.IsNullOrWhiteSpace(ageRaw) AndAlso Integer.TryParse(ageRaw, ageValue) Then
                        AddAgeValue(ageValue, result)
                        Continue For
                    End If

                    Dim userId = row("user_id")?.ToString()
                    Dim birthdate As DateTime?
                    If Not String.IsNullOrWhiteSpace(userId) AndAlso profiles.ContainsKey(userId) Then
                        birthdate = profiles(userId).Birthdate
                    End If
                    AddAge(birthdate, result)
                Next
            Catch
                useFallback = True
            End Try
            If useFallback Then Return Await LoadAgeCountsAsync("checkins", "user_id", profiles)
            Return result
        End Function

        Private Async Function LoadAgeCountsAsync(table As String,
                                                  userColumn As String,
                                                  profiles As Dictionary(Of String, ProfileDemographic)) As Task(Of AgeStats)
            Dim result As New AgeStats()
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(
                    $"{table}?select={userColumn}&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&limit=10000")
                For Each row In JArray.Parse(raw)
                    Dim userId = row(userColumn)?.ToString()
                    Dim birthdate As DateTime?
                    If Not String.IsNullOrWhiteSpace(userId) AndAlso profiles.ContainsKey(userId) Then
                        birthdate = profiles(userId).Birthdate
                    End If
                    AddAge(birthdate, result)
                Next
            Catch
            End Try
            Return result
        End Function

        Private Async Function LoadEventsAsync(s As BarStats) As Task
            Dim rows As New List(Of EventRow)()

            Try
                Dim official = Await SupabaseClient.GetListAsync(Of OfficialEvent)(
                    $"official_events?select=*&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&order=start_at.desc")
                For Each ev In official
                    rows.Add(New EventRow With {
                        .Type = "Officiel",
                        .Title = ev.Title,
                        .Status = ev.StatusLabel,
                        .Date = ev.DateLabel
                    })
                Next
            Catch
            End Try

            Try
                Dim ephemeral = Await SupabaseClient.GetListAsync(Of EphemeralEvent)(
                    $"ephemeral_events?select=*&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&order=start_at.desc")
                For Each ev In ephemeral
                    rows.Add(New EventRow With {
                        .Type = If(ev.Visibility = "public", "Sortie publique", "Sortie privée"),
                        .Title = ev.Title,
                        .Status = If(ev.IsActive, ev.Status, "inactive"),
                        .Date = ev.DateLabel
                    })
                Next
            Catch
            End Try

            s.Events = rows.OrderByDescending(Function(x) x.Date).ToList()
        End Function

        Private Async Function LoadCheckinRowsAsync(s As BarStats, profiles As Dictionary(Of String, ProfileDemographic)) As Task
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(
                    $"checkins?select=*&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&order=checked_in_at.desc&limit=300")
                For Each row As JObject In JArray.Parse(raw)
                    Dim p = LookupProfile(profiles, TextValue(row, "user_id"))
                    s.Checkins.Add(New CheckinRow With {
                        .Entree = DateLabel(row, "checked_in_at"),
                        .Sortie = DateLabel(row, "checked_out_at"),
                        .Utilisateur = p.Display,
                        .Actif = If(BoolValue(row, "is_active"), "Oui", "Non"),
                        .Evenement = TextValue(row, "event_id"),
                        .Genre = If(TextValue(row, "gender_snapshot") <> "—", TextValue(row, "gender_snapshot"), If(p.Gender, "—")),
                        .Age = If(TextValue(row, "age_snapshot") <> "—", TextValue(row, "age_snapshot"), AgeLabel(p.Birthdate)),
                        .Tranche = TextValue(row, "age_band_snapshot")
                    })
                Next
            Catch
            End Try
        End Function

        Private Async Function LoadViewRowsAsync(s As BarStats, profiles As Dictionary(Of String, ProfileDemographic)) As Task
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(
                    $"bar_profile_views?select=*&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&order=created_at.desc&limit=300")
                For Each row As JObject In JArray.Parse(raw)
                    Dim p = LookupProfile(profiles, FirstText(row, "viewer_id", "user_id", "profile_id"))
                    s.Views.Add(New ViewRow With {
                        .Date = DateLabel(row, "created_at"),
                        .Utilisateur = p.Display,
                        .Genre = If(p.Gender, "—"),
                        .Age = AgeLabel(p.Birthdate),
                        .Source = FirstText(row, "source", "screen", "referrer")
                    })
                Next
            Catch
            End Try
        End Function

        Private Async Function LoadFollowerRowsAsync(s As BarStats, profiles As Dictionary(Of String, ProfileDemographic)) As Task
            Try
                Dim raw = Await SupabaseClient.GetRawAsync(
                    $"bar_followers?select=*&bar_id=eq.{Uri.EscapeDataString(_bar.Id)}&order=created_at.desc&limit=300")
                For Each row As JObject In JArray.Parse(raw)
                    Dim p = LookupProfile(profiles, FirstText(row, "user_id", "profile_id", "follower_id"))
                    s.FollowersRows.Add(New FollowerRow With {
                        .Date = DateLabel(row, "created_at"),
                        .Utilisateur = p.Display,
                        .Genre = If(p.Gender, "—"),
                        .Age = AgeLabel(p.Birthdate)
                    })
                Next
            Catch
            End Try
        End Function

        Private Sub AddGender(gender As String, result As GenderStats)
            Select Case If(gender, String.Empty).Trim().ToLowerInvariant()
                Case "femme", "female", "woman"
                    result.Female += 1
                Case "homme", "male", "man"
                    result.Male += 1
                Case Else
                    result.Unknown += 1
            End Select
        End Sub

        Private Sub AddAge(birthdate As DateTime?, result As AgeStats)
            If Not birthdate.HasValue Then
                result.Unknown += 1
                Return
            End If

            Dim age = CInt(Math.Floor((DateTime.Today - birthdate.Value.Date).TotalDays / 365.25))
            AddAgeValue(age, result)
        End Sub

        Private Sub AddAgeValue(age As Integer, result As AgeStats)
            Select Case age
                Case 18 To 24
                    result.Age18To24 += 1
                Case 25 To 34
                    result.Age25To34 += 1
                Case 35 To 44
                    result.Age35To44 += 1
                Case Is >= 45
                    result.Age45Plus += 1
                Case Else
                    result.Unknown += 1
            End Select
        End Sub

        Private Sub AddAgeBand(ageBand As String, result As AgeStats)
            Select Case If(ageBand, String.Empty).Trim().ToLowerInvariant()
                Case "18_24", "18-24"
                    result.Age18To24 += 1
                Case "25_34", "25-34"
                    result.Age25To34 += 1
                Case "35_44", "35-44"
                    result.Age35To44 += 1
                Case "45_plus", "45+", "45"
                    result.Age45Plus += 1
                Case Else
                    result.Unknown += 1
            End Select
        End Sub

        Private Function MakeCard(title As String, value As Integer, accent As Color) As Panel
            Dim card As New Panel() With {
                .Size = New Size(210, 88),
                .Margin = New Padding(8),
                .BackColor = NightOutTheme.BgPanel
            }

            Dim stripe As New Panel() With {.Dock = DockStyle.Left, .Width = 5, .BackColor = accent}
            Dim lblValue As New Label() With {
                .Text = value.ToString("N0"),
                .ForeColor = accent,
                .Font = NightOutTheme.FontTitle(20.0F),
                .Location = New Point(16, 4),
                .Size = New Size(184, 44),
                .TextAlign = ContentAlignment.MiddleLeft
            }
            Dim lblCaption As New Label() With {
                .Text = title,
                .ForeColor = NightOutTheme.Muted,
                .Font = NightOutTheme.FontBody(8.5F),
                .Location = New Point(18, 50),
                .Size = New Size(184, 30)
            }

            card.Controls.Add(lblValue)
            card.Controls.Add(lblCaption)
            card.Controls.Add(stripe)
            Return card
        End Function

        Private Function StatusLabel(status As String) As String
            Select Case status
                Case "approved" : Return "Validé"
                Case "rejected" : Return "Refusé"
                Case Else : Return "En attente"
            End Select
        End Function

        Private Function LookupProfile(profiles As Dictionary(Of String, ProfileDemographic), userId As String) As ProfileDemographic
            If Not String.IsNullOrWhiteSpace(userId) AndAlso userId <> "-" AndAlso profiles IsNot Nothing AndAlso profiles.ContainsKey(userId) Then
                Return profiles(userId)
            End If
            Return New ProfileDemographic With {.Display = If(String.IsNullOrWhiteSpace(userId), "-", userId)}
        End Function

        Private Function TextValue(row As JObject, key As String) As String
            If row Is Nothing OrElse String.IsNullOrWhiteSpace(key) OrElse row(key) Is Nothing OrElse row(key).Type = JTokenType.Null Then Return "-"
            Dim value = row(key).ToString()
            If String.IsNullOrWhiteSpace(value) Then Return "-"
            Return value
        End Function

        Private Function FirstText(row As JObject, ParamArray keys As String()) As String
            For Each key In keys
                Dim value = TextValue(row, key)
                If value <> "-" Then Return value
            Next
            Return "-"
        End Function

        Private Function BoolValue(row As JObject, key As String) As Boolean
            Dim value = TextValue(row, key)
            Dim parsed As Boolean
            If Boolean.TryParse(value, parsed) Then Return parsed
            Return False
        End Function

        Private Function DateLabel(row As JObject, key As String) As String
            Dim value = TextValue(row, key)
            Dim parsed As DateTime
            If DateTime.TryParse(value, parsed) Then Return DateLabel(parsed)
            Return value
        End Function

        Private Function DateLabel(value As DateTime) As String
            If value = DateTime.MinValue Then Return "-"
            Return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        End Function

        Private Function AgeLabel(birthdate As DateTime?) As String
            If Not birthdate.HasValue Then Return "-"
            Dim age = CInt(Math.Floor((DateTime.Today - birthdate.Value.Date).TotalDays / 365.25))
            If age < 0 OrElse age > 120 Then Return "-"
            Return age.ToString()
        End Function
        Private Class BarStats
            Public Property ProfileViewsTotal As Integer
            Public Property ProfileViewsFemale As Integer
            Public Property ProfileViewsMale As Integer
            Public Property ProfileViewsUnknown As Integer
            Public Property CheckinsTotal As Integer
            Public Property CheckinsFemale As Integer
            Public Property CheckinsMale As Integer
            Public Property CheckinsUnknown As Integer
            Public Property CheckinsAge18To24 As Integer
            Public Property CheckinsAge25To34 As Integer
            Public Property CheckinsAge35To44 As Integer
            Public Property CheckinsAge45Plus As Integer
            Public Property CheckinsAgeUnknown As Integer
            Public Property ActiveCheckins As Integer
            Public Property Followers As Integer
            Public Property OfficialEvents As Integer
            Public Property EphemeralEvents As Integer
            Public Property InfoRows As New List(Of InfoRow)
            Public Property Hours As New List(Of HourRow)
            Public Property Events As New List(Of EventRow)
            Public Property Checkins As New List(Of CheckinRow)
            Public Property Views As New List(Of ViewRow)
            Public Property FollowersRows As New List(Of FollowerRow)
        End Class

        Private Class GenderStats
            Public Property Female As Integer
            Public Property Male As Integer
            Public Property Unknown As Integer
        End Class

        Private Class AgeStats
            Public Property Age18To24 As Integer
            Public Property Age25To34 As Integer
            Public Property Age35To44 As Integer
            Public Property Age45Plus As Integer
            Public Property Unknown As Integer
        End Class

        Private Class ProfileDemographic
            Public Property Display As String
            Public Property Gender As String
            Public Property Birthdate As DateTime?
        End Class

        Private Class InfoRow
            Public Property Champ As String
            Public Property Valeur As String
        End Class

        Private Class HourRow
            Public Property Jour As String
            Public Property Statut As String
            Public Property Ouverture As String
            Public Property Fermeture As String
        End Class

        Private Class EventRow
            Public Property Type As String
            Public Property Title As String
            Public Property Status As String
            Public Property [Date] As String
        End Class

        Private Class CheckinRow
            Public Property Entree As String
            Public Property Sortie As String
            Public Property Utilisateur As String
            Public Property Actif As String
            Public Property Evenement As String
            Public Property Genre As String
            Public Property Age As String
            Public Property Tranche As String
        End Class

        Private Class ViewRow
            Public Property [Date] As String
            Public Property Utilisateur As String
            Public Property Genre As String
            Public Property Age As String
            Public Property Source As String
        End Class

        Private Class FollowerRow
            Public Property [Date] As String
            Public Property Utilisateur As String
            Public Property Genre As String
            Public Property Age As String
        End Class

    End Class

End Namespace
