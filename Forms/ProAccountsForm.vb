Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    ''' <summary>
    ''' Patrons d'établissement / organisateurs.
    ''' Source = table profiles (account_type establishment/organizer ou is_pro),
    ''' car la ligne professional_accounts n'est créée que lorsque le patron
    ''' ouvre son espace pro dans l'app. Le statut pro est porté par profiles.professional_status.
    ''' </summary>
    Public Class ProAccountsForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly pnlTop As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly cboStatus As New ComboBox()
        Private ReadOnly grid As New DataGridView()

        Private ReadOnly pnlDetail As New Panel()
        Private ReadOnly lblName As New Label()
        Private ReadOnly lblInfo As New Label()
        Private ReadOnly lblBarsHeader As New Label()
        Private ReadOnly lstBars As New ListBox()
        Private ReadOnly btnApprove As New Button()
        Private ReadOnly btnPartner As New Button()
        Private ReadOnly btnSuspend As New Button()
        Private ReadOnly btnReject As New Button()

        Private _all As New List(Of Profile)()
        Private _view As New List(Of Profile)()
        Private _accountBars As New List(Of Bar)()
        Private _cityMap As New Dictionary(Of String, String)()

        Public Sub New()
            Me.Text = "Patrons d'établissement"
            Me.BackColor = NightOutTheme.BgDark

            ' Barre haute
            pnlTop.Dock = DockStyle.Top
            pnlTop.Height = 92
            pnlTop.BackColor = NightOutTheme.BgPanel

            lblTitle.Text = "🏢  Patrons d'établissement"
            lblTitle.ForeColor = NightOutTheme.Gold
            lblTitle.Font = NightOutTheme.FontTitle(13.0F)
            lblTitle.AutoSize = True
            lblTitle.Location = New Point(14, 12)
            pnlTop.Controls.Add(lblTitle)

            cboStatus.Location = New Point(14, 50)
            cboStatus.Size = New Size(220, 28)
            cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
            cboStatus.FlatStyle = FlatStyle.Flat
            cboStatus.BackColor = NightOutTheme.BgPanel2
            cboStatus.ForeColor = NightOutTheme.Cream
            cboStatus.Items.AddRange(New Object() {"Tous", "En attente", "Validés", "Partenaires", "Suspendus", "Refusés"})
            cboStatus.SelectedIndex = 0
            pnlTop.Controls.Add(cboStatus)

            ' Détail à droite
            pnlDetail.Dock = DockStyle.Right
            pnlDetail.Width = 400
            pnlDetail.BackColor = NightOutTheme.BgPanel
            pnlDetail.Padding = New Padding(18)

            lblName.Text = "Sélectionnez un patron"
            lblName.ForeColor = NightOutTheme.Gold
            lblName.Font = NightOutTheme.FontTitle(14.0F)
            lblName.Dock = DockStyle.Top
            lblName.Height = 50

            lblInfo.ForeColor = NightOutTheme.Cream
            lblInfo.Font = NightOutTheme.FontBody(9.5F)
            lblInfo.Dock = DockStyle.Top
            lblInfo.Height = 170

            lblBarsHeader.Text = "Établissements"
            lblBarsHeader.ForeColor = NightOutTheme.Gold
            lblBarsHeader.Font = NightOutTheme.FontTitle(10.0F)
            lblBarsHeader.Dock = DockStyle.Top
            lblBarsHeader.Height = 26

            lstBars.Dock = DockStyle.Top
            lstBars.Height = 170
            lstBars.BorderStyle = BorderStyle.FixedSingle
            lstBars.BackColor = NightOutTheme.BgPanel2
            lstBars.ForeColor = NightOutTheme.Cream
            lstBars.Font = NightOutTheme.FontBody(9.5F)
            lstBars.IntegralHeight = False

            NightOutTheme.StylePrimaryButton(btnApprove, NightOutTheme.Green)
            btnApprove.Text = "✓  Valider"
            btnApprove.Dock = DockStyle.Bottom
            btnApprove.Height = 40
            btnApprove.Enabled = False

            NightOutTheme.StyleGhostButton(btnPartner, NightOutTheme.Gold)
            btnPartner.Text = "★  Marquer Partenaire"
            btnPartner.Dock = DockStyle.Bottom
            btnPartner.Height = 38
            btnPartner.Enabled = False

            NightOutTheme.StyleGhostButton(btnSuspend, NightOutTheme.Orange)
            btnSuspend.Text = "⏸  Suspendre"
            btnSuspend.Dock = DockStyle.Bottom
            btnSuspend.Height = 38
            btnSuspend.Enabled = False

            NightOutTheme.StyleGhostButton(btnReject, NightOutTheme.Red)
            btnReject.Text = "✕  Refuser"
            btnReject.Dock = DockStyle.Bottom
            btnReject.Height = 38
            btnReject.Enabled = False

            ' Ordre d'ajout = empilement inverse pour Dock=Top
            ' (haut→bas : Nom, Infos, "Établissements", liste)
            pnlDetail.Controls.Add(lstBars)
            pnlDetail.Controls.Add(lblBarsHeader)
            pnlDetail.Controls.Add(lblInfo)
            pnlDetail.Controls.Add(lblName)
            pnlDetail.Controls.Add(SpacerBottom(8))
            pnlDetail.Controls.Add(btnApprove)
            pnlDetail.Controls.Add(SpacerBottom(6))
            pnlDetail.Controls.Add(btnPartner)
            pnlDetail.Controls.Add(SpacerBottom(6))
            pnlDetail.Controls.Add(btnSuspend)
            pnlDetail.Controls.Add(SpacerBottom(6))
            pnlDetail.Controls.Add(btnReject)

            ' Grille
            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.Columns.Add(NewCol("Name", "Nom"))
            grid.Columns.Add(NewCol("Kind", "Type"))
            grid.Columns.Add(NewCol("City", "Ville"))
            grid.Columns.Add(NewCol("Status", "Statut"))
            grid.Columns.Add(NewCol("Verified", "Vérifié"))
            grid.Columns("Kind").FillWeight = 55
            grid.Columns("Status").FillWeight = 55
            grid.Columns("Verified").FillWeight = 45

            Me.Controls.Add(grid)
            Me.Controls.Add(pnlDetail)
            Me.Controls.Add(pnlTop)

            AddHandler cboStatus.SelectedIndexChanged, AddressOf Filter_Changed
            AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
            AddHandler lstBars.DoubleClick, AddressOf Bars_DoubleClick
            AddHandler btnApprove.Click, Async Sub() Await SetStatus("approved")
            AddHandler btnPartner.Click, Async Sub() Await SetStatus("partner")
            AddHandler btnSuspend.Click, Async Sub() Await SetStatus("suspended")
            AddHandler btnReject.Click, Async Sub() Await SetStatus("rejected")
        End Sub

        Private Function SpacerBottom(h As Integer) As Panel
            Return New Panel() With {.Dock = DockStyle.Bottom, .Height = h, .BackColor = NightOutTheme.BgPanel}
        End Function

        Private Shared Function NewCol(name As String, header As String) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header}
        End Function

        Private Async Sub ProAccountsForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Me.UseWaitCursor = True
                ' Villes (pour résoudre city_id -> nom)
                Try
                    Dim cities = Await RefService.GetCitiesAsync()
                    _cityMap = cities.GroupBy(Function(c) c.Id).ToDictionary(
                        Function(g) g.Key, Function(g) g.First().Name)
                Catch
                    _cityMap = New Dictionary(Of String, String)()
                End Try

                _all = Await ProOwnerService.GetAllAsync()
                ApplyFilter()
            Catch ex As Exception
                MessageBox.Show("Erreur : " & ex.Message, "Patrons", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Sub Filter_Changed(sender As Object, e As EventArgs)
            ApplyFilter()
        End Sub

        Private Function CityName(cityId As String) As String
            If Not String.IsNullOrEmpty(cityId) AndAlso _cityMap.ContainsKey(cityId) Then Return _cityMap(cityId)
            Return "—"
        End Function

        Private Sub ApplyFilter()
            Dim sf As String = Nothing
            Select Case cboStatus.SelectedIndex
                Case 1 : sf = "pending"
                Case 2 : sf = "approved"
                Case 3 : sf = "partner"
                Case 4 : sf = "suspended"
                Case 5 : sf = "rejected"
            End Select

            Dim filtered = _all.AsEnumerable()
            If sf IsNot Nothing Then filtered = filtered.Where(Function(p) p.ProfessionalStatus = sf)
            _view = filtered.ToList()

            grid.Rows.Clear()
            For Each p In _view
                grid.Rows.Add(p.NameDisplay, p.ProKindLabel, CityName(p.CityId),
                              p.ProStatusLabel, If(p.IsVerified, "✔", "—"))
            Next
            lblTitle.Text = $"🏢  Patrons d'établissement ({_view.Count})"
            ClearDetail()
        End Sub

        Private Sub ClearDetail()
            lblName.Text = "Sélectionnez un patron"
            lblInfo.Text = ""
            lblBarsHeader.Text = "Établissements"
            lstBars.Items.Clear()
            _accountBars = New List(Of Bar)()
            EnableActions(False)
        End Sub

        Private Sub EnableActions(enable As Boolean)
            btnApprove.Enabled = enable
            btnPartner.Enabled = enable
            btnSuspend.Enabled = enable
            btnReject.Enabled = enable
        End Sub

        Private Function Selected() As Profile
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim idx = grid.CurrentRow.Index
            If idx < 0 OrElse idx >= _view.Count Then Return Nothing
            Return _view(idx)
        End Function

        Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
            Dim p = Selected()
            If p Is Nothing Then ClearDetail() : Return
            lblName.Text = p.NameDisplay
            lblInfo.Text =
                $"🏷 {p.ProKindLabel}" & vbCrLf &
                $"📌 Statut pro : {p.ProStatusLabel}" & vbCrLf & vbCrLf &
                $"👤 Pseudo : {If(p.Username, "—")}" & vbCrLf &
                $"📍 Ville : {CityName(p.CityId)}" & vbCrLf &
                $"✔ Vérifié : {If(p.IsVerified, "oui", "non")}" & vbCrLf &
                $"📅 Inscrit le {p.CreatedAt.ToLocalTime():dd/MM/yyyy}" & vbCrLf &
                $"🆔 {p.Id}"
            EnableActions(True)

            Dim t = LoadEstablishments(p)
        End Sub

        Private Async Function LoadEstablishments(p As Profile) As Task
            lblBarsHeader.Text = "Établissements…"
            lstBars.Items.Clear()
            _accountBars = New List(Of Bar)()
            Try
                ' Établissements rattachés au patron (owner_id = id du profil)
                _accountBars = Await BarService.GetForProAccountAsync(Nothing, p.Id)
                For Each b In _accountBars
                    Dim premium = If(b.IsPremium, " ★", "")
                    lstBars.Items.Add($"{b.Name}  ·  {b.StatusLabel}{premium}")
                Next
                lblBarsHeader.Text = $"Établissements ({_accountBars.Count})  —  double-clic pour ouvrir"
                If _accountBars.Count = 0 Then lstBars.Items.Add("(aucun établissement)")
            Catch ex As Exception
                lblBarsHeader.Text = "Établissements"
                lstBars.Items.Add("Erreur : " & ex.Message)
            End Try
        End Function

        Private Sub Bars_DoubleClick(sender As Object, e As EventArgs)
            Dim idx = lstBars.SelectedIndex
            If idx < 0 OrElse idx >= _accountBars.Count Then Return
            Dim b = _accountBars(idx)
            Using f As New BarEditForm(b)
                If f.ShowDialog(Me) = DialogResult.OK Then
                    Dim p = Selected()
                    If p IsNot Nothing Then
                        Dim t = LoadEstablishments(p)
                    End If
                End If
            End Using
        End Sub

        Private Async Function SetStatus(status As String) As Task
            Dim p = Selected() : If p Is Nothing Then Return
            Try
                EnableActions(False)
                Me.UseWaitCursor = True
                Await ProOwnerService.SetStatusAsync(p.Id, status)
                Await RefreshDataAsync()
                Dim parent = TryCast(Me.MdiParent, MainForm)
                If parent IsNot Nothing Then Await parent.RefreshPendingBadgeAsync()
            Catch ex As Exception
                MessageBox.Show("Échec : " & ex.Message, "Action", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

    End Class

End Namespace
