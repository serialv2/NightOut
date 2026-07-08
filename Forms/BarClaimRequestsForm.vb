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
    ''' Administration des demandes de récupération/revendication des fiches établissements.
    ''' Permet de rattacher un bar créé par l'admin au vrai compte professionnel.
    ''' </summary>
    Public Class BarClaimRequestsForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly pnlTop As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly cboStatus As New ComboBox()
        Private ReadOnly grid As New DataGridView()

        Private ReadOnly pnlDetail As New Panel()
        Private ReadOnly lblName As New Label()
        Private ReadOnly lblInfo As New Label()
        Private ReadOnly txtProof As New TextBox()
        Private ReadOnly txtAdminNote As New TextBox()
        Private ReadOnly btnApprove As New Button()
        Private ReadOnly btnReject As New Button()
        Private ReadOnly btnOpenBar As New Button()

        Private _requests As New List(Of BarClaimRequest)()

        Public Sub New()
            Me.Text = "Demandes de récupération"
            Me.BackColor = NightOutTheme.BgDark

            pnlTop.Dock = DockStyle.Top
            pnlTop.Height = 92
            pnlTop.BackColor = NightOutTheme.BgPanel

            lblTitle.Text = "🔐  Demandes de récupération d'établissement"
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
            cboStatus.Items.AddRange(New Object() {"En attente", "Toutes", "Validées", "Refusées", "Annulées"})
            cboStatus.SelectedIndex = 0
            pnlTop.Controls.Add(cboStatus)

            pnlDetail.Dock = DockStyle.Right
            pnlDetail.Width = 430
            pnlDetail.BackColor = NightOutTheme.BgPanel
            pnlDetail.Padding = New Padding(18)

            lblName.Text = "Sélectionnez une demande"
            lblName.ForeColor = NightOutTheme.Gold
            lblName.Font = NightOutTheme.FontTitle(14.0F)
            lblName.Dock = DockStyle.Top
            lblName.Height = 58

            lblInfo.ForeColor = NightOutTheme.Cream
            lblInfo.Font = NightOutTheme.FontBody(9.5F)
            lblInfo.Dock = DockStyle.Top
            lblInfo.Height = 190

            txtProof.Multiline = True
            txtProof.ReadOnly = True
            txtProof.ScrollBars = ScrollBars.Vertical
            txtProof.BorderStyle = BorderStyle.FixedSingle
            txtProof.BackColor = NightOutTheme.BgPanel2
            txtProof.ForeColor = NightOutTheme.Cream
            txtProof.Font = NightOutTheme.FontBody(9.0F)
            txtProof.Dock = DockStyle.Top
            txtProof.Height = 150

            txtAdminNote.Multiline = True
            txtAdminNote.ScrollBars = ScrollBars.Vertical
            txtAdminNote.BorderStyle = BorderStyle.FixedSingle
            txtAdminNote.BackColor = NightOutTheme.BgPanel2
            txtAdminNote.ForeColor = NightOutTheme.Cream
            txtAdminNote.Font = NightOutTheme.FontBody(9.0F)
            txtAdminNote.Dock = DockStyle.Top
            txtAdminNote.Height = 90
            txtAdminNote.PlaceholderText = "Note admin facultative..."

            NightOutTheme.StyleGhostButton(btnOpenBar, NightOutTheme.Blue)
            btnOpenBar.Text = "👁  Ouvrir la fiche bar"
            btnOpenBar.Dock = DockStyle.Bottom
            btnOpenBar.Height = 38
            btnOpenBar.Enabled = False

            NightOutTheme.StylePrimaryButton(btnApprove, NightOutTheme.Green)
            btnApprove.Text = "✓  Valider la récupération"
            btnApprove.Dock = DockStyle.Bottom
            btnApprove.Height = 42
            btnApprove.Enabled = False

            NightOutTheme.StyleGhostButton(btnReject, NightOutTheme.Red)
            btnReject.Text = "✕  Refuser la demande"
            btnReject.Dock = DockStyle.Bottom
            btnReject.Height = 40
            btnReject.Enabled = False

            pnlDetail.Controls.Add(txtAdminNote)
            pnlDetail.Controls.Add(txtProof)
            pnlDetail.Controls.Add(lblInfo)
            pnlDetail.Controls.Add(lblName)
            pnlDetail.Controls.Add(SpacerBottom(8))
            pnlDetail.Controls.Add(btnApprove)
            pnlDetail.Controls.Add(SpacerBottom(6))
            pnlDetail.Controls.Add(btnReject)
            pnlDetail.Controls.Add(SpacerBottom(6))
            pnlDetail.Controls.Add(btnOpenBar)

            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.Columns.Add(NewCol("Bar", "Établissement"))
            grid.Columns.Add(NewCol("Requester", "Demandeur"))
            grid.Columns.Add(NewCol("Pro", "Compte pro"))
            grid.Columns.Add(NewCol("Status", "Statut"))
            grid.Columns.Add(NewCol("Created", "Demandé le"))
            grid.Columns("Bar").FillWeight = 95
            grid.Columns("Requester").FillWeight = 70
            grid.Columns("Pro").FillWeight = 75
            grid.Columns("Status").FillWeight = 45
            grid.Columns("Created").FillWeight = 60

            Me.Controls.Add(grid)
            Me.Controls.Add(pnlDetail)
            Me.Controls.Add(pnlTop)

            AddHandler cboStatus.SelectedIndexChanged, Async Sub() Await RefreshDataAsync()
            AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
            AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
            AddHandler btnApprove.Click, Async Sub() Await ApproveSelectedAsync()
            AddHandler btnReject.Click, Async Sub() Await RejectSelectedAsync()
            AddHandler btnOpenBar.Click, AddressOf OpenSelectedBar
        End Sub

        Private Function SpacerBottom(h As Integer) As Panel
            Return New Panel() With {.Dock = DockStyle.Bottom, .Height = h, .BackColor = NightOutTheme.BgPanel}
        End Function

        Private Shared Function NewCol(name As String, header As String) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header}
        End Function

        Private Async Sub BarClaimRequestsForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Me.UseWaitCursor = True
                Dim status As String = "pending"
                Select Case cboStatus.SelectedIndex
                    Case 1 : status = "all"
                    Case 2 : status = "approved"
                    Case 3 : status = "rejected"
                    Case 4 : status = "cancelled"
                End Select

                _requests = Await BarClaimService.GetAllAsync(status)
                grid.Rows.Clear()
                For Each r In _requests
                    grid.Rows.Add(If(r.BarName, r.BarId), If(r.ContactName, If(r.RequesterName, "—")),
                                  If(r.ProName, r.ProfessionalAccountId), r.StatusLabel, r.CreatedLabel)
                Next
                lblTitle.Text = $"🔐  Demandes de récupération d'établissement ({_requests.Count})"
                ClearDetail()
            Catch ex As Exception
                MessageBox.Show("Erreur de chargement : " & ex.Message, "Récupération établissement",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Sub ClearDetail()
            lblName.Text = "Sélectionnez une demande"
            lblInfo.Text = ""
            txtProof.Text = ""
            txtAdminNote.Text = ""
            EnableActions(False)
        End Sub

        Private Sub EnableActions(enable As Boolean)
            btnOpenBar.Enabled = enable
            Dim r = Selected()
            Dim canReview = enable AndAlso r IsNot Nothing AndAlso r.Status = "pending"
            btnApprove.Enabled = canReview
            btnReject.Enabled = canReview
        End Sub

        Private Function Selected() As BarClaimRequest
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim idx = grid.CurrentRow.Index
            If idx < 0 OrElse idx >= _requests.Count Then Return Nothing
            Return _requests(idx)
        End Function

        Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
            Dim r = Selected()
            If r Is Nothing Then ClearDetail() : Return

            lblName.Text = If(r.BarName, "Établissement")
            lblInfo.Text =
                $"📍 {If(r.BarAddress, "—")}" & vbCrLf & vbCrLf &
                $"👤 Demandeur : {If(r.ContactName, If(r.RequesterName, "—"))}" & vbCrLf &
                $"🏷 Rôle : {If(r.Role, "—")}" & vbCrLf &
                $"📞 Téléphone : {If(r.Phone, "—")}" & vbCrLf &
                $"🏢 Compte pro : {If(r.ProName, r.ProfessionalAccountId)}" & vbCrLf &
                $"📌 Statut : {r.StatusLabel}" & vbCrLf &
                $"📅 Demande : {r.CreatedLabel}" & vbCrLf &
                $"🆔 {r.Id}"

            txtProof.Text =
                "Message / justificatif :" & vbCrLf &
                If(String.IsNullOrWhiteSpace(r.ProofMessage), "—", r.ProofMessage) & vbCrLf & vbCrLf &
                "Fichier preuve :" & vbCrLf &
                If(String.IsNullOrWhiteSpace(r.ProofFileUrl), "—", r.ProofFileUrl) & vbCrLf & vbCrLf &
                "Note admin existante :" & vbCrLf &
                If(String.IsNullOrWhiteSpace(r.AdminNote), "—", r.AdminNote)

            txtAdminNote.Text = ""
            EnableActions(True)
        End Sub

        Private Async Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Await OpenSelectedBarAsync()
        End Sub

        Private Sub OpenSelectedBar(sender As Object, e As EventArgs)
            Dim t = OpenSelectedBarAsync()
        End Sub

        Private Async Function OpenSelectedBarAsync() As Task
            Dim r = Selected()
            If r Is Nothing OrElse String.IsNullOrEmpty(r.BarId) Then Return
            Try
                Dim b = Await BarService.GetByIdAsync(r.BarId)
                If b Is Nothing Then
                    MessageBox.Show("Bar introuvable.", "Établissement", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If
                Using f As New BarEditForm(b)
                    f.ShowDialog(Me)
                End Using
            Catch ex As Exception
                MessageBox.Show("Impossible d'ouvrir le bar : " & ex.Message, "Établissement",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Function

        Private Async Function ApproveSelectedAsync() As Task
            Dim r = Selected()
            If r Is Nothing OrElse r.Status <> "pending" Then Return

            If MessageBox.Show(
                $"Valider la récupération de « {If(r.BarName, r.BarId)} » par le compte pro « {If(r.ProName, r.ProfessionalAccountId)} » ?" & vbCrLf & vbCrLf &
                "Le bar sera rattaché à ce compte pro et les autres demandes en attente seront refusées.",
                "Validation récupération", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return

            Await DoAction(Function() BarClaimService.ApproveAsync(r.Id, txtAdminNote.Text), "Demande validée.")
        End Function

        Private Async Function RejectSelectedAsync() As Task
            Dim r = Selected()
            If r Is Nothing OrElse r.Status <> "pending" Then Return

            If MessageBox.Show($"Refuser la demande pour « {If(r.BarName, r.BarId)} » ?",
                "Refus récupération", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return

            Await DoAction(Function() BarClaimService.RejectAsync(r.Id, txtAdminNote.Text), "Demande refusée.")
        End Function

        Private Async Function DoAction(action As Func(Of Task(Of Boolean)), okMsg As String) As Task
            Try
                EnableActions(False)
                Me.UseWaitCursor = True
                Await action()
                MessageBox.Show(okMsg, "NightOut Admin", MessageBoxButtons.OK, MessageBoxIcon.Information)
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
