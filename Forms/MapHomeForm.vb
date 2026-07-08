Option Strict Off
Option Explicit On

Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports System.Drawing
Imports Microsoft.Web.WebView2.WinForms
Imports Microsoft.Web.WebView2.Core
Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    ''' <summary>
    ''' Carte d'accueil : bars validés / à valider, événements, présences,
    ''' via WebView2 + Leaflet (OpenStreetMap, gratuit, sans token).
    ''' </summary>
    Public Class MapHomeForm
        Inherits Form
        Implements IRefreshable

        Private WithEvents web As WebView2
        Private ReadOnly pnlHeader As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly lblHint As New Label()
        Private _mapReady As Boolean = False
        Private _pendingData As String = Nothing

        Public Sub New()
            Me.Text = "Carte NightOut"
            Me.BackColor = NightOutTheme.BgDark

            pnlHeader.Dock = DockStyle.Top
            pnlHeader.Height = 48
            pnlHeader.BackColor = NightOutTheme.BgPanel

            lblTitle.Text = "🗺  Carte d'administration"
            lblTitle.ForeColor = NightOutTheme.Gold
            lblTitle.Font = NightOutTheme.FontTitle(12.0F)
            lblTitle.AutoSize = True
            lblTitle.Location = New Point(14, 12)
            pnlHeader.Controls.Add(lblTitle)

            lblHint.Text = "Cliquez un marqueur orange pour valider un bar"
            lblHint.ForeColor = NightOutTheme.Muted
            lblHint.Font = NightOutTheme.FontBody(8.5F)
            lblHint.AutoSize = True
            lblHint.Location = New Point(300, 16)
            pnlHeader.Controls.Add(lblHint)

            web = New WebView2() With {.Dock = DockStyle.Fill}
            Me.Controls.Add(web)
            Me.Controls.Add(pnlHeader)
        End Sub

        Private Async Sub MapHomeForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Try
                Await web.EnsureCoreWebView2Async()
                AddHandler web.CoreWebView2.WebMessageReceived, AddressOf OnWebMessage

                Dim htmlPath = Path.Combine(Application.StartupPath, "Resources", "map.html")
                If File.Exists(htmlPath) Then
                    Dim html = File.ReadAllText(htmlPath)
                    web.CoreWebView2.NavigateToString(html)
                Else
                    web.CoreWebView2.NavigateToString("<body style='background:#0A1018;color:#E5484D;font-family:sans-serif'>" &
                        "<p>map.html introuvable dans le dossier Resources.</p></body>")
                End If
            Catch ex As Exception
                MessageBox.Show("WebView2 n'a pas pu démarrer." & vbCrLf &
                    "Installez le runtime « Microsoft Edge WebView2 » (gratuit)." & vbCrLf & vbCrLf & ex.Message,
                    "Carte", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        Private Sub OnWebMessage(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs)
            Try
                Dim raw = e.TryGetWebMessageAsString()
                Dim msg = JObject.Parse(raw)
                Dim type = msg("type")?.ToString()

                Select Case type
                    Case "ready"
                        _mapReady = True
                        If _pendingData IsNot Nothing Then
                            PushData(_pendingData)
                            _pendingData = Nothing
                        Else
                            Me.BeginInvoke(Sub() LoadMapDataFireAndForget())
                        End If

                    Case "validate"
                        ' Ouvre l'écran de validation des bars
                        Dim parent = TryCast(Me.MdiParent, MainForm)
                        parent?.NavigateToValidation()

                    Case "open"
                        ' Ouvre la fiche éditable du bar
                        Dim id = msg("id")?.ToString()
                        If Not String.IsNullOrEmpty(id) Then
                            Me.BeginInvoke(Sub() OpenBarStats(id))
                        End If
                End Select
            Catch
            End Try
        End Sub

        Private Sub LoadMapDataFireAndForget()
            Dim t = RefreshDataAsync()
        End Sub

        ''' <summary>Ouvre la fiche stats d'un bar, puis rafraîchit la carte si modification.</summary>
        Private Async Sub OpenBarStats(barId As String)
            Try
                Dim bar = Await BarService.GetByIdAsync(barId)
                If bar Is Nothing Then
                    MessageBox.Show("Bar introuvable.", "Fiche bar", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
                Using f As New BarStatsDetailForm(bar)
                    If f.ShowDialog(Me) = DialogResult.OK Then
                        Await RefreshDataAsync()
                        Dim parent = TryCast(Me.MdiParent, MainForm)
                        If parent IsNot Nothing Then Await parent.RefreshPendingBadgeAsync()
                    End If
                End Using
            Catch ex As Exception
                MessageBox.Show("Erreur : " & ex.Message, "Fiche bar", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Dim payload = New JObject()

                ' ── Bars (validés + en attente, avec coordonnées) ──
                Dim bars = Await BarService.GetAllAsync()
                Dim barsArr = New JArray()
                For Each b In bars.Where(Function(x) x.Latitude <> 0 AndAlso x.Longitude <> 0)
                    barsArr.Add(New JObject(
                        New JProperty("id", b.Id),
                        New JProperty("name", b.Name),
                        New JProperty("address", b.Address),
                        New JProperty("status", b.Status),
                        New JProperty("lat", b.Latitude),
                        New JProperty("lng", b.Longitude)))
                Next
                payload.Add("bars", barsArr)

                ' ── Événements publiés en cours / à venir ──
                Dim evtArr = New JArray()
                Try
                    Dim events = Await SupabaseClient.GetListAsync(Of Models.OfficialEvent)(
                        "official_events?select=*&status=eq.published")
                    For Each ev In events.Where(Function(x) x.Latitude.GetValueOrDefault() <> 0 AndAlso x.Longitude.GetValueOrDefault() <> 0)
                        evtArr.Add(New JObject(
                            New JProperty("id", ev.Id),
                            New JProperty("title", ev.Title),
                            New JProperty("date", ev.DateLabel),
                            New JProperty("kind", "Officiel"),
                            New JProperty("lat", ev.Latitude.Value),
                            New JProperty("lng", ev.Longitude.Value)))
                    Next
                Catch
                End Try

                Try
                    Dim ephemeralEvents = Await SupabaseClient.GetListAsync(Of Models.EphemeralEvent)(
                        "ephemeral_events?select=*&status=eq.published&is_active=eq.true")
                    For Each ev In ephemeralEvents.Where(Function(x) x.Latitude.GetValueOrDefault() <> 0 AndAlso x.Longitude.GetValueOrDefault() <> 0)
                        evtArr.Add(New JObject(
                            New JProperty("id", ev.Id),
                            New JProperty("title", ev.Title),
                            New JProperty("date", ev.DateLabel),
                            New JProperty("kind", If(ev.Visibility = "public", "Sortie publique", "Sortie privée")),
                            New JProperty("lat", ev.Latitude.Value),
                            New JProperty("lng", ev.Longitude.Value)))
                    Next
                Catch
                End Try
                payload.Add("events", evtArr)

                ' ── Présences récentes non secrètes (best effort) ──
                Dim usersArr = New JArray()
                Try
                    Dim sinceUtc = DateTime.UtcNow.AddHours(-6).ToString("o")
                    Dim raw = Await SupabaseClient.GetRawAsync(
                        $"presences?select=user_id,latitude,longitude,is_secret,updated_at&is_secret=eq.false&updated_at=gte.{Uri.EscapeDataString(sinceUtc)}&limit=300")
                    Dim arr = JArray.Parse(raw)
                    For Each p In arr
                        Dim lat = p("latitude")?.ToObject(Of Double)()
                        Dim lng = p("longitude")?.ToObject(Of Double)()
                        If lat.HasValue AndAlso lng.HasValue AndAlso lat.Value <> 0 AndAlso lng.Value <> 0 Then
                            usersArr.Add(New JObject(
                                New JProperty("name", "Utilisateur"),
                                New JProperty("lat", lat.Value),
                                New JProperty("lng", lng.Value)))
                        End If
                    Next
                Catch
                End Try
                payload.Add("users", usersArr)

                Dim json = payload.ToString(Newtonsoft.Json.Formatting.None)
                If _mapReady Then
                    PushData(json)
                Else
                    _pendingData = json
                End If

                lblHint.Text = $"{barsArr.Count} bar(s) · {evtArr.Count} événement(s) · {usersArr.Count} présence(s)"
            Catch ex As Exception
                lblHint.Text = "Erreur de chargement : " & ex.Message
                lblHint.ForeColor = NightOutTheme.Red
            End Try
        End Function

        Private Async Sub PushData(json As String)
            Try
                ' Encode en littéral JS sûr
                Dim escaped = Newtonsoft.Json.JsonConvert.ToString(json)
                Await web.CoreWebView2.ExecuteScriptAsync($"setData({escaped})")
            Catch
            End Try
        End Sub

    End Class

End Namespace
