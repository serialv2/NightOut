Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Net.Http
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    ''' <summary>
    ''' Fiche d'un bar (façon page bar de l'app MAUI) éditable par l'admin :
    ''' couverture, infos, contacts, coordonnées, statut, options.
    ''' Renvoie DialogResult.OK si une modification a été enregistrée.
    ''' </summary>
    Public Class BarEditForm
        Inherits Form

        Private Shared ReadOnly _imgHttp As New HttpClient()
        Private ReadOnly _bar As Bar

        Private ReadOnly picCover As New PictureBox()
        Private ReadOnly picLogo As New PictureBox()
        Private ReadOnly lblName As New Label()
        Private ReadOnly lblBadge As New Label()
        Private ReadOnly content As New FlowLayoutPanel()

        Private ReadOnly txtName As New TextBox()
        Private ReadOnly txtCategory As New TextBox()
        Private ReadOnly txtAddress As New TextBox()
        Private ReadOnly txtCity As New TextBox()
        Private ReadOnly txtPhone As New TextBox()
        Private ReadOnly txtWebsite As New TextBox()
        Private ReadOnly txtInstagram As New TextBox()
        Private ReadOnly txtLat As New TextBox()
        Private ReadOnly txtLng As New TextBox()
        Private ReadOnly txtDesc As New TextBox()
        Private ReadOnly cboStatus As New ComboBox()
        Private ReadOnly chkActive As New CheckBox()
        Private ReadOnly chkPremium As New CheckBox()
        Private ReadOnly chkVerified As New CheckBox()

        Private ReadOnly btnSave As New Button()
        Private ReadOnly btnCancel As New Button()

        ' Horaires : index 0..6 = lundi..dimanche
        Private ReadOnly _hourClosed(6) As CheckBox
        Private ReadOnly _hourOpen(6) As DateTimePicker
        Private ReadOnly _hourClose(6) As DateTimePicker

        Public Sub New(bar As Bar)
            _bar = bar

            Me.Text = "Fiche bar — " & If(bar.Name, "")
            Me.BackColor = NightOutTheme.BgDark
            Me.FormBorderStyle = FormBorderStyle.Sizable
            Me.StartPosition = FormStartPosition.CenterParent
            Me.ClientSize = New Size(560, 760)
            Me.MinimumSize = New Size(480, 560)

            ' ── Couverture ──
            picCover.Dock = DockStyle.Top
            picCover.Height = 180
            picCover.BackColor = NightOutTheme.BgPanel2
            picCover.SizeMode = PictureBoxSizeMode.Zoom

            ' Logo rond superposé en bas à gauche de la bannière (façon page bar MAUI)
            picLogo.Size = New Size(78, 78)
            picLogo.SizeMode = PictureBoxSizeMode.Zoom
            picLogo.BackColor = NightOutTheme.BgPanel
            picLogo.BorderStyle = BorderStyle.None
            picLogo.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
            picLogo.Location = New Point(16, picCover.Height - picLogo.Height - 10)
            Dim gp As New System.Drawing.Drawing2D.GraphicsPath()
            gp.AddEllipse(0, 0, picLogo.Width, picLogo.Height)
            picLogo.Region = New Region(gp)
            picCover.Controls.Add(picLogo)

            lblName.Text = If(bar.Name, "(sans nom)")
            lblName.ForeColor = NightOutTheme.Gold
            lblName.Font = NightOutTheme.FontTitle(15.0F)
            lblName.Dock = DockStyle.Top
            lblName.Height = 38
            lblName.TextAlign = ContentAlignment.MiddleLeft
            lblName.Padding = New Padding(20, 0, 0, 0)
            lblName.BackColor = NightOutTheme.BgPanel

            lblBadge.Dock = DockStyle.Top
            lblBadge.Height = 24
            lblBadge.Font = NightOutTheme.FontBody(8.5F)
            lblBadge.TextAlign = ContentAlignment.MiddleLeft
            lblBadge.Padding = New Padding(20, 0, 0, 0)
            lblBadge.BackColor = NightOutTheme.BgPanel
            lblBadge.ForeColor = NightOutTheme.Muted

            ' ── Zone scrollable de champs ──
            content.Dock = DockStyle.Fill
            content.AutoScroll = True
            content.FlowDirection = FlowDirection.TopDown
            content.WrapContents = False
            content.BackColor = NightOutTheme.BgDark
            content.Padding = New Padding(20, 14, 20, 14)

            StyleInput(txtName)
            StyleInput(txtCategory)
            StyleInput(txtAddress)
            StyleInput(txtCity)
            StyleInput(txtPhone)
            StyleInput(txtWebsite)
            StyleInput(txtInstagram)
            StyleInput(txtLat)
            StyleInput(txtLng)
            StyleInput(txtDesc)
            txtDesc.Multiline = True
            txtDesc.ScrollBars = ScrollBars.Vertical

            cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
            cboStatus.FlatStyle = FlatStyle.Flat
            cboStatus.BackColor = NightOutTheme.BgPanel2
            cboStatus.ForeColor = NightOutTheme.Cream
            cboStatus.Font = NightOutTheme.FontBody(10.0F)
            cboStatus.Items.AddRange(New Object() {"En attente", "Validé", "Refusé"})

            StyleCheck(chkActive, "Actif (visible dans l'app)")
            StyleCheck(chkPremium, "Premium ★")
            StyleCheck(chkVerified, "Vérifié ✔")

            content.Controls.Add(FieldRow("Nom", txtName))
            content.Controls.Add(FieldRow("Catégorie (séparées par des virgules)", txtCategory))
            content.Controls.Add(FieldRow("Adresse", txtAddress))
            content.Controls.Add(FieldRow("Ville", txtCity))
            content.Controls.Add(FieldRow("Téléphone", txtPhone))
            content.Controls.Add(FieldRow("Site web", txtWebsite))
            content.Controls.Add(FieldRow("Instagram", txtInstagram))
            content.Controls.Add(TwoFieldRow("Latitude", txtLat, "Longitude", txtLng))
            content.Controls.Add(FieldRow("Description", txtDesc, 90))
            content.Controls.Add(FieldRow("Statut", cboStatus))
            content.Controls.Add(OptionsRow())
            content.Controls.Add(BuildHoursSection())

            ' ── Bas : enregistrer / annuler ──
            Dim pnlBottom As New Panel() With {.Dock = DockStyle.Bottom, .Height = 60, .BackColor = NightOutTheme.BgPanel, .Padding = New Padding(20, 12, 20, 12)}

            NightOutTheme.StylePrimaryButton(btnSave, NightOutTheme.Green)
            btnSave.Text = "💾  Enregistrer"
            btnSave.Width = 200
            btnSave.Dock = DockStyle.Right

            NightOutTheme.StyleGhostButton(btnCancel, NightOutTheme.Muted)
            btnCancel.Text = "Annuler"
            btnCancel.Width = 120
            btnCancel.Dock = DockStyle.Left
            btnCancel.DialogResult = DialogResult.Cancel

            pnlBottom.Controls.Add(btnSave)
            pnlBottom.Controls.Add(btnCancel)

            Me.Controls.Add(content)
            Me.Controls.Add(pnlBottom)
            Me.Controls.Add(lblBadge)
            Me.Controls.Add(lblName)
            Me.Controls.Add(picCover)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel

            AddHandler btnSave.Click, AddressOf Save_Click

            BindFromBar()
        End Sub

        ' ── Helpers UI ──
        Private Sub StyleInput(tb As TextBox)
            tb.BackColor = NightOutTheme.BgPanel2
            tb.ForeColor = NightOutTheme.Cream
            tb.BorderStyle = BorderStyle.FixedSingle
            tb.Font = NightOutTheme.FontBody(10.0F)
        End Sub

        Private Sub StyleCheck(chk As CheckBox, caption As String)
            chk.Text = caption
            chk.ForeColor = NightOutTheme.Cream
            chk.Font = NightOutTheme.FontBody(9.5F)
            chk.AutoSize = True
        End Sub

        Private Function FieldRow(caption As String, ctrl As Control, Optional ctrlHeight As Integer = 26) As Panel
            Dim p As New Panel() With {
                .Width = 496,
                .Height = 22 + ctrlHeight + 6,
                .BackColor = NightOutTheme.BgDark,
                .Margin = New Padding(0, 0, 0, 8)
            }
            Dim lbl As New Label() With {
                .Text = caption,
                .ForeColor = NightOutTheme.Muted,
                .Font = NightOutTheme.FontBody(8.5F),
                .Dock = DockStyle.Top,
                .Height = 20
            }
            ctrl.Dock = DockStyle.Fill
            p.Controls.Add(ctrl)
            p.Controls.Add(lbl)
            Return p
        End Function

        Private Function TwoFieldRow(cap1 As String, c1 As Control, cap2 As String, c2 As Control) As Panel
            Dim p As New Panel() With {.Width = 496, .Height = 54, .BackColor = NightOutTheme.BgDark, .Margin = New Padding(0, 0, 0, 8)}
            Dim left As New Panel() With {.Width = 240, .Height = 54, .Dock = DockStyle.Left, .BackColor = NightOutTheme.BgDark}
            Dim right As New Panel() With {.Width = 240, .Height = 54, .Dock = DockStyle.Right, .BackColor = NightOutTheme.BgDark}
            left.Controls.Add(c1) : c1.Dock = DockStyle.Fill
            left.Controls.Add(NewCap(cap1))
            right.Controls.Add(c2) : c2.Dock = DockStyle.Fill
            right.Controls.Add(NewCap(cap2))
            p.Controls.Add(right)
            p.Controls.Add(left)
            Return p
        End Function

        Private Function NewCap(caption As String) As Label
            Return New Label() With {
                .Text = caption,
                .ForeColor = NightOutTheme.Muted,
                .Font = NightOutTheme.FontBody(8.5F),
                .Dock = DockStyle.Top,
                .Height = 20
            }
        End Function

        Private Function OptionsRow() As Panel
            Dim p As New Panel() With {.Width = 496, .Height = 96, .BackColor = NightOutTheme.BgDark, .Margin = New Padding(0, 4, 0, 8)}
            chkActive.Location = New Point(2, 6)
            chkPremium.Location = New Point(2, 34)
            chkVerified.Location = New Point(2, 62)
            p.Controls.Add(chkActive)
            p.Controls.Add(chkPremium)
            p.Controls.Add(chkVerified)
            Return p
        End Function

        Private Function BuildHoursSection() As Panel
            Dim names = BarOpeningHour.DayNames
            Dim rowH = 32
            Dim wrap As New Panel() With {
                .Width = 496,
                .Height = 30 + 7 * rowH + 8,
                .BackColor = NightOutTheme.BgDark,
                .Margin = New Padding(0, 6, 0, 8)
            }

            Dim title As New Label() With {
                .Text = "Horaires d'ouverture",
                .ForeColor = NightOutTheme.Gold,
                .Font = NightOutTheme.FontTitle(10.5F),
                .Dock = DockStyle.Top,
                .Height = 28
            }
            wrap.Controls.Add(title)

            Dim rows As New Panel() With {.Dock = DockStyle.Fill, .BackColor = NightOutTheme.BgDark}

            For i = 0 To 6
                Dim idx = i ' capture pour les lambdas
                Dim row As New Panel() With {
                    .Width = 470, .Height = rowH - 2, .Top = i * rowH, .Left = 0,
                    .BackColor = NightOutTheme.BgPanel
                }
                Dim lbl As New Label() With {
                    .Text = names(i), .ForeColor = NightOutTheme.Cream, .Font = NightOutTheme.FontBody(9.5F),
                    .Left = 10, .Top = 7, .Width = 78, .AutoSize = False
                }
                Dim chk As New CheckBox() With {
                    .Text = "Fermé", .ForeColor = NightOutTheme.Muted, .Font = NightOutTheme.FontBody(8.5F),
                    .Left = 92, .Top = 6, .Width = 70, .AutoSize = False
                }
                Dim dOpen As New DateTimePicker() With {
                    .Format = DateTimePickerFormat.Custom, .CustomFormat = "HH:mm", .ShowUpDown = True,
                    .Left = 168, .Top = 4, .Width = 64
                }
                Dim sep As New Label() With {
                    .Text = "→", .ForeColor = NightOutTheme.Muted, .Left = 238, .Top = 7, .Width = 18, .AutoSize = False
                }
                Dim dClose As New DateTimePicker() With {
                    .Format = DateTimePickerFormat.Custom, .CustomFormat = "HH:mm", .ShowUpDown = True,
                    .Left = 260, .Top = 4, .Width = 64
                }

                ' Valeurs par défaut (18:00 → 02:00)
                dOpen.Value = DateTime.Today.AddHours(18)
                dClose.Value = DateTime.Today.AddHours(2)

                AddHandler chk.CheckedChanged, Sub(s, ev)
                                                   dOpen.Enabled = Not chk.Checked
                                                   dClose.Enabled = Not chk.Checked
                                               End Sub

                row.Controls.Add(lbl)
                row.Controls.Add(chk)
                row.Controls.Add(dOpen)
                row.Controls.Add(sep)
                row.Controls.Add(dClose)
                rows.Controls.Add(row)

                _hourClosed(idx) = chk
                _hourOpen(idx) = dOpen
                _hourClose(idx) = dClose
            Next

            wrap.Controls.Add(rows)
            ' title (Dock Top) doit rester au-dessus de rows (Dock Fill)
            rows.BringToFront()
            title.BringToFront()
            Return wrap
        End Function

        ''' <summary>Convertit "HH:mm:ss" en DateTime (sur la date du jour).</summary>
        Private Shared Function ParseTimeToDate(timeStr As String) As DateTime
            Dim ts As TimeSpan
            If TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, ts) Then
                Return DateTime.Today.Add(ts)
            End If
            Return DateTime.Today.AddHours(18)
        End Function

        ' ── Données ──
        Private Sub BindFromBar()
            txtName.Text = If(_bar.Name, "")
            txtCategory.Text = If(_bar.Category, "")
            txtAddress.Text = If(_bar.Address, "")
            txtCity.Text = If(_bar.AddressCityName, "")
            txtPhone.Text = If(_bar.Phone, "")
            txtWebsite.Text = If(_bar.Website, "")
            txtInstagram.Text = If(_bar.Instagram, "")
            txtLat.Text = _bar.Latitude.ToString(CultureInfo.InvariantCulture)
            txtLng.Text = _bar.Longitude.ToString(CultureInfo.InvariantCulture)
            txtDesc.Text = If(_bar.Description, "")
            cboStatus.SelectedIndex = StatusToIndex(_bar.Status)
            chkActive.Checked = _bar.IsActive
            chkPremium.Checked = _bar.IsPremium
            chkVerified.Checked = _bar.IsVerified

            lblBadge.Text = $"ID : {_bar.Id}   ·   créé le {_bar.CreatedAt.ToLocalTime():dd/MM/yyyy}"
        End Sub

        Private Shared Function StatusToIndex(status As String) As Integer
            Select Case status
                Case "approved" : Return 1
                Case "rejected" : Return 2
                Case Else : Return 0
            End Select
        End Function

        Private Shared Function IndexToStatus(idx As Integer) As String
            Select Case idx
                Case 1 : Return "approved"
                Case 2 : Return "rejected"
                Case Else : Return "pending"
            End Select
        End Function

        ''' <summary>Parse une coordonnée en tolérant la virgule décimale française.</summary>
        Private Shared Function ParseCoord(text As String, ByRef value As Double) As Boolean
            If String.IsNullOrWhiteSpace(text) Then Return False
            Dim normalized = text.Trim().Replace(",", ".")
            Return Double.TryParse(normalized, NumberStyles.Float Or NumberStyles.AllowLeadingSign,
                                   CultureInfo.InvariantCulture, value)
        End Function

        Private Async Sub Save_Click(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtName.Text) Then
                MessageBox.Show("Le nom est obligatoire.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim lat As Double, lng As Double
            If Not ParseCoord(txtLat.Text, lat) Then
                MessageBox.Show("Latitude invalide (ex. 50.6292).", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            If Not ParseCoord(txtLng.Text, lng) Then
                MessageBox.Show("Longitude invalide (ex. 3.0573).", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            ' Reporter les saisies dans le modèle
            _bar.Name = txtName.Text.Trim()
            _bar.Category = NullIfEmpty(txtCategory.Text)
            _bar.Address = NullIfEmpty(txtAddress.Text)
            _bar.AddressCityName = NullIfEmpty(txtCity.Text)
            _bar.Phone = NullIfEmpty(txtPhone.Text)
            _bar.Website = NullIfEmpty(txtWebsite.Text)
            _bar.Instagram = NullIfEmpty(txtInstagram.Text)
            _bar.Description = NullIfEmpty(txtDesc.Text)
            _bar.Latitude = lat
            _bar.Longitude = lng
            _bar.Status = IndexToStatus(cboStatus.SelectedIndex)
            _bar.IsActive = chkActive.Checked
            _bar.IsPremium = chkPremium.Checked
            _bar.IsVerified = chkVerified.Checked

            Try
                btnSave.Enabled = False
                Me.UseWaitCursor = True
                Await BarService.UpdateAsync(_bar)

                ' Horaires (table séparée) — n'empêche pas l'enregistrement du bar si ça échoue
                Try
                    Dim hrs As New List(Of BarOpeningHour)()
                    For i = 0 To 6
                        hrs.Add(New BarOpeningHour() With {
                            .BarId = _bar.Id,
                            .DayOfWeek = i + 1,
                            .IsClosed = _hourClosed(i).Checked,
                            .OpenTime = _hourOpen(i).Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                            .CloseTime = _hourClose(i).Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                        })
                    Next
                    Await HoursService.SaveAsync(_bar.Id, hrs)
                Catch hex As Exception
                    MessageBox.Show(
                        "Le bar a été enregistré, mais les horaires n'ont pas pu être sauvegardés." & vbCrLf &
                        "Vérifie qu'une policy RLS autorise les admins à écrire dans bar_opening_hours." & vbCrLf & vbCrLf &
                        hex.Message, "Horaires", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                End Try

                Me.DialogResult = DialogResult.OK
                Me.Close()
            Catch ex As Exception
                MessageBox.Show("Échec de l'enregistrement : " & ex.Message, "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                btnSave.Enabled = True
                Me.UseWaitCursor = False
            End Try
        End Sub

        Private Shared Function NullIfEmpty(s As String) As String
            If String.IsNullOrWhiteSpace(s) Then Return Nothing
            Return s.Trim()
        End Function

        ' ── Bannière + logo (chargés après affichage) ──
        Private Async Sub BarEditForm_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
            If Not String.IsNullOrEmpty(_bar.CoverUrl) Then
                Await LoadInto(picCover, _bar.CoverUrl)
            ElseIf Not String.IsNullOrEmpty(_bar.LogoUrl) Then
                ' à défaut de bannière, on met le logo en fond
                Await LoadInto(picCover, _bar.LogoUrl)
            End If

            If Not String.IsNullOrEmpty(_bar.LogoUrl) Then
                Await LoadInto(picLogo, _bar.LogoUrl)
            Else
                picLogo.Visible = False
            End If
        End Sub

        ''' <summary>Télécharge une image distante dans un PictureBox (copie indépendante du flux).</summary>
        Private Async Function LoadInto(pic As PictureBox, url As String) As Task
            Try
                Dim bytes = Await _imgHttp.GetByteArrayAsync(url)
                Using ms As New MemoryStream(bytes)
                    Using tmp = Image.FromStream(ms)
                        pic.Image = New Bitmap(tmp)
                    End Using
                End Using
            Catch
                ' image indisponible : on garde le fond uni
            End Try
        End Function

        ' ── Horaires (chargés après affichage) ──
        Private Async Sub LoadHours() Handles MyBase.Shown
            Try
                Dim list = Await HoursService.GetForBarAsync(_bar.Id)
                If list Is Nothing OrElse list.Count = 0 Then Return
                Dim map = list.GroupBy(Function(h) h.DayOfWeek).ToDictionary(
                    Function(g) g.Key, Function(g) g.First())
                For i = 0 To 6
                    Dim dow = i + 1
                    If Not map.ContainsKey(dow) Then Continue For
                    Dim h = map(dow)
                    _hourClosed(i).Checked = h.IsClosed
                    If Not String.IsNullOrEmpty(h.OpenTime) Then _hourOpen(i).Value = ParseTimeToDate(h.OpenTime)
                    If Not String.IsNullOrEmpty(h.CloseTime) Then _hourClose(i).Value = ParseTimeToDate(h.CloseTime)
                    _hourOpen(i).Enabled = Not h.IsClosed
                    _hourClose(i).Enabled = Not h.IsClosed
                Next
            Catch
                ' table absente ou lecture impossible : on garde les valeurs par défaut
            End Try
        End Sub

    End Class

End Namespace
