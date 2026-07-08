Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    Public Class MessageReportsForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly pnlTop As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly cboStatus As New ComboBox()
        Private ReadOnly btnReload As New Button()
        Private ReadOnly grid As New DataGridView()

        Private ReadOnly pnlDetail As New Panel()
        Private ReadOnly lblReportTitle As New Label()
        Private ReadOnly lblInfo As New Label()
        Private ReadOnly txtMessage As New TextBox()
        Private ReadOnly txtNote As New TextBox()
        Private ReadOnly btnReviewed As New Button()
        Private ReadOnly btnDismiss As New Button()
        Private ReadOnly btnActionTaken As New Button()
        Private ReadOnly btnCopyIds As New Button()

        Private _reports As New List(Of MessageReport)()

        Public Sub New()
            Me.Text = "Signalements"
            Me.BackColor = NightOutTheme.BgDark

            pnlTop.Dock = DockStyle.Top
            pnlTop.Height = 76
            pnlTop.BackColor = NightOutTheme.BgPanel

            lblTitle.Text = "Signalements de messages"
            lblTitle.ForeColor = NightOutTheme.Red
            lblTitle.Font = NightOutTheme.FontTitle(13.0F)
            lblTitle.AutoSize = True
            lblTitle.Location = New Point(14, 12)
            pnlTop.Controls.Add(lblTitle)

            cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
            cboStatus.Items.AddRange(New Object() {
                New StatusItem("A traiter", "pending"),
                New StatusItem("Tous", "all"),
                New StatusItem("Traites", "reviewed"),
                New StatusItem("Ignores", "dismissed"),
                New StatusItem("Action prise", "action_taken")
            })
            cboStatus.SelectedIndex = 0
            cboStatus.Location = New Point(14, 42)
            cboStatus.Width = 180
            pnlTop.Controls.Add(cboStatus)

            NightOutTheme.StylePrimaryButton(btnReload, NightOutTheme.Blue)
            btnReload.Text = "Rafraichir"
            btnReload.Location = New Point(206, 39)
            btnReload.Size = New Size(120, 30)
            pnlTop.Controls.Add(btnReload)

            pnlDetail.Dock = DockStyle.Right
            pnlDetail.Width = 420
            pnlDetail.BackColor = NightOutTheme.BgPanel
            pnlDetail.Padding = New Padding(16)

            lblReportTitle.Text = "Selectionnez un signalement"
            lblReportTitle.ForeColor = NightOutTheme.Gold
            lblReportTitle.Font = NightOutTheme.FontTitle(13.0F)
            lblReportTitle.Dock = DockStyle.Top
            lblReportTitle.Height = 42

            lblInfo.ForeColor = NightOutTheme.Cream
            lblInfo.Font = NightOutTheme.FontBody(9.0F)
            lblInfo.Dock = DockStyle.Top
            lblInfo.Height = 150

            txtMessage.Multiline = True
            txtMessage.ReadOnly = True
            txtMessage.ScrollBars = ScrollBars.Vertical
            txtMessage.BackColor = NightOutTheme.BgPanel2
            txtMessage.ForeColor = NightOutTheme.Cream
            txtMessage.BorderStyle = BorderStyle.FixedSingle
            txtMessage.Font = NightOutTheme.FontBody(9.5F)
            txtMessage.Dock = DockStyle.Top
            txtMessage.Height = 130

            txtNote.Multiline = True
            txtNote.ScrollBars = ScrollBars.Vertical
            txtNote.BackColor = NightOutTheme.BgPanel2
            txtNote.ForeColor = NightOutTheme.Cream
            txtNote.BorderStyle = BorderStyle.FixedSingle
            txtNote.Font = NightOutTheme.FontBody(9.0F)
            txtNote.Dock = DockStyle.Top
            txtNote.Height = 90

            NightOutTheme.StylePrimaryButton(btnActionTaken, NightOutTheme.Red)
            btnActionTaken.Text = "Bannir l'utilisateur signale"
            btnActionTaken.ForeColor = NightOutTheme.Cream
            btnActionTaken.Dock = DockStyle.Bottom
            btnActionTaken.Height = 44
            btnActionTaken.Enabled = False

            NightOutTheme.StyleGhostButton(btnDismiss, NightOutTheme.Muted)
            btnDismiss.Text = "Ignorer + repondre au signalant"
            btnDismiss.ForeColor = NightOutTheme.Cream
            btnDismiss.Dock = DockStyle.Bottom
            btnDismiss.Height = 42
            btnDismiss.Enabled = False

            NightOutTheme.StyleGhostButton(btnReviewed, NightOutTheme.Green)
            btnReviewed.Text = "Avertir avant ban"
            btnReviewed.ForeColor = NightOutTheme.Cream
            btnReviewed.Dock = DockStyle.Bottom
            btnReviewed.Height = 42
            btnReviewed.Enabled = False

            NightOutTheme.StyleGhostButton(btnCopyIds, NightOutTheme.Blue)
            btnCopyIds.Text = "Copier les IDs"
            btnCopyIds.ForeColor = NightOutTheme.Cream
            btnCopyIds.Dock = DockStyle.Bottom
            btnCopyIds.Height = 36
            btnCopyIds.Enabled = False

            pnlDetail.Controls.Add(txtNote)
            pnlDetail.Controls.Add(txtMessage)
            pnlDetail.Controls.Add(lblInfo)
            pnlDetail.Controls.Add(lblReportTitle)
            pnlDetail.Controls.Add(New Panel() With {.Dock = DockStyle.Bottom, .Height = 8, .BackColor = NightOutTheme.BgPanel})
            pnlDetail.Controls.Add(btnActionTaken)
            pnlDetail.Controls.Add(New Panel() With {.Dock = DockStyle.Bottom, .Height = 8, .BackColor = NightOutTheme.BgPanel})
            pnlDetail.Controls.Add(btnDismiss)
            pnlDetail.Controls.Add(btnReviewed)
            pnlDetail.Controls.Add(New Panel() With {.Dock = DockStyle.Bottom, .Height = 8, .BackColor = NightOutTheme.BgPanel})
            pnlDetail.Controls.Add(btnCopyIds)

            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.Columns.Add(NewCol("Created", "Date"))
            grid.Columns.Add(NewCol("Reason", "Raison"))
            grid.Columns.Add(NewCol("Reporter", "Signale par"))
            grid.Columns.Add(NewCol("Reported", "Utilisateur signale"))
            grid.Columns.Add(NewCol("Warnings", "Avert."))
            grid.Columns.Add(NewCol("Snippet", "Message"))
            grid.Columns.Add(NewCol("Status", "Statut"))
            grid.Columns("Created").FillWeight = 70
            grid.Columns("Reason").FillWeight = 70
            grid.Columns("Status").FillWeight = 60
            grid.Columns("Snippet").FillWeight = 130
            grid.Columns("Warnings").FillWeight = 40

            Me.Controls.Add(grid)
            Me.Controls.Add(pnlDetail)
            Me.Controls.Add(pnlTop)

            AddHandler Me.Load, AddressOf MessageReportsForm_Load
            AddHandler btnReload.Click, AddressOf Reload_Click
            AddHandler cboStatus.SelectedIndexChanged, AddressOf Status_Changed
            AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
            AddHandler grid.CellClick, AddressOf Grid_CellClick
            AddHandler grid.CellEnter, AddressOf Grid_CellEnter
            AddHandler btnReviewed.Click, AddressOf Reviewed_Click
            AddHandler btnDismiss.Click, AddressOf Dismiss_Click
            AddHandler btnActionTaken.Click, AddressOf ActionTaken_Click
            AddHandler btnCopyIds.Click, AddressOf CopyIds_Click
        End Sub

        Private Shared Function NewCol(name As String, header As String) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header}
        End Function

        Private Async Sub MessageReportsForm_Load(sender As Object, e As EventArgs)
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Await LoadReportsAsync()
        End Function

        Private Async Function LoadReportsAsync() As Task
            Try
                Me.UseWaitCursor = True
                Dim status = DirectCast(cboStatus.SelectedItem, StatusItem).Value
                _reports = Await MessageReportService.GetAllAsync(status)

                grid.Rows.Clear()
                For Each r In _reports
                    grid.Rows.Add(r.CreatedLabel, r.ReasonLabel,
                                  If(String.IsNullOrWhiteSpace(r.ReporterName), r.ReporterId, r.ReporterName),
                                  If(String.IsNullOrWhiteSpace(r.ReportedUserName), r.ReportedUserId, r.ReportedUserName),
                                  If(r.ReportedUserIsBanned, "BANNI", r.ReportedUserWarningCount.ToString()),
                                  ShortText(r.MessageContentSnapshot, 90),
                                  r.StatusLabel)
                Next

                lblTitle.Text = $"Signalements de messages ({_reports.Count})"
                If _reports.Count > 0 AndAlso grid.Rows.Count > 0 Then
                    grid.ClearSelection()
                    grid.CurrentCell = grid.Rows(0).Cells(0)
                    grid.Rows(0).Selected = True
                    ShowReport(_reports(0))
                Else
                    ClearDetail()
                End If
            Catch ex As Exception
                MessageBox.Show("Erreur de chargement : " & ex.Message, "Signalements",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Function Selected() As MessageReport
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim idx = grid.CurrentRow.Index
            If idx < 0 OrElse idx >= _reports.Count Then Return Nothing
            Return _reports(idx)
        End Function

        Private Sub ClearDetail()
            lblReportTitle.Text = "Selectionnez un signalement"
            lblInfo.Text = ""
            txtMessage.Text = ""
            txtNote.Text = ""
            SetActionsEnabled(False)
        End Sub

        Private Sub SetActionsEnabled(enabled As Boolean)
            btnReviewed.Enabled = enabled
            btnDismiss.Enabled = enabled
            btnActionTaken.Enabled = enabled
            btnCopyIds.Enabled = enabled

            btnActionTaken.ForeColor = If(enabled, NightOutTheme.Cream, NightOutTheme.Muted)
            btnDismiss.ForeColor = If(enabled, NightOutTheme.Cream, NightOutTheme.Muted)
            btnReviewed.ForeColor = If(enabled, NightOutTheme.Cream, NightOutTheme.Muted)
            btnCopyIds.ForeColor = If(enabled, NightOutTheme.Cream, NightOutTheme.Muted)
        End Sub

        Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
            Dim r = Selected()
            If r Is Nothing Then ClearDetail() : Return
            ShowReport(r)
        End Sub

        Private Sub Grid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            If e.RowIndex >= _reports.Count Then Return
            grid.Rows(e.RowIndex).Selected = True
            ShowReport(_reports(e.RowIndex))
        End Sub

        Private Sub Grid_CellEnter(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            If e.RowIndex >= _reports.Count Then Return
            ShowReport(_reports(e.RowIndex))
        End Sub

        Private Sub ShowReport(r As MessageReport)
            lblReportTitle.Text = r.ReasonLabel
            lblInfo.Text =
                $"Date : {r.CreatedLabel}" & vbCrLf &
                $"Statut : {r.StatusLabel}" & vbCrLf & vbCrLf &
                $"Signale par : {If(String.IsNullOrWhiteSpace(r.ReporterName), r.ReporterId, r.ReporterName)}" & vbCrLf &
                $"Utilisateur signale : {If(String.IsNullOrWhiteSpace(r.ReportedUserName), r.ReportedUserId, r.ReportedUserName)}" & vbCrLf &
                $"Avertissements deja recus : {r.ReportedUserWarningCount}" & If(r.ReportedUserIsBanned, " (compte deja banni)", "") & vbCrLf &
                $"Message ID : {r.DirectMessageId}"

            txtMessage.Text = If(String.IsNullOrWhiteSpace(r.MessageContentSnapshot), "(Message vide ou media)", r.MessageContentSnapshot)
            txtNote.Text = If(r.AdminNote, "")
            SetActionsEnabled(True)
        End Sub

        Private Shared Function ShortText(value As String, maxLength As Integer) As String
            If String.IsNullOrWhiteSpace(value) Then Return "(Message vide ou media)"
            Dim cleaned = value.Replace(vbCr, " ").Replace(vbLf, " ").Trim()
            If cleaned.Length <= maxLength Then Return cleaned
            Return cleaned.Substring(0, maxLength - 1) & "..."
        End Function

        Private Async Sub Reload_Click(sender As Object, e As EventArgs)
            Await LoadReportsAsync()
        End Sub

        Private Async Sub Status_Changed(sender As Object, e As EventArgs)
            If Me.IsHandleCreated Then Await LoadReportsAsync()
        End Sub

        Private Async Sub Reviewed_Click(sender As Object, e As EventArgs)
            Dim r = Selected()
            If r Is Nothing Then Return

            Dim msg = Interaction.InputBox(
                "Message d'avertissement envoye a l'utilisateur qui a ecrit le message signale :",
                "Avertissement avant ban",
                "Votre message a ete signale et examine par l'equipe Spotiz. Ce comportement n'est pas accepte sur l'application. En cas de recidive, votre compte pourra etre banni.")

            If String.IsNullOrWhiteSpace(msg) Then Return
            Await DoModerationActionAsync(Function() MessageReportService.WarnReportedUserAsync(r, msg.Trim()))
        End Sub

        Private Async Sub Dismiss_Click(sender As Object, e As EventArgs)
            Dim r = Selected()
            If r Is Nothing Then Return

            Dim msg = Interaction.InputBox(
                "Message envoye a l'utilisateur qui a fait le signalement :",
                "Ignorer le signalement",
                "Merci pour votre signalement. Apres verification, nous n'avons pas retenu d'action de moderation pour ce message.")

            If String.IsNullOrWhiteSpace(msg) Then Return
            Await DoModerationActionAsync(Function() MessageReportService.DismissWithReporterMessageAsync(r, msg.Trim()))
        End Sub

        Private Async Sub ActionTaken_Click(sender As Object, e As EventArgs)
            Dim r = Selected()
            If r Is Nothing Then Return

            Dim reason = Interaction.InputBox(
                "Raison du ban envoyee a l'utilisateur banni :",
                "Bannir l'utilisateur",
                "Votre compte a ete banni suite a un message contraire aux regles de Spotiz.")

            If String.IsNullOrWhiteSpace(reason) Then Return

            Dim reporterMsg = Interaction.InputBox(
                "Message envoye a l'utilisateur qui a fait le signalement :",
                "Informer le signalant",
                "Merci pour votre signalement. Apres verification, une action de moderation a ete prise.")

            If String.IsNullOrWhiteSpace(reporterMsg) Then Return

            If MessageBox.Show("Confirmer le ban de l'utilisateur signale ?", "Bannir",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return

            Await DoModerationActionAsync(Function() MessageReportService.BanReportedUserAsync(r, reason.Trim(), reporterMsg.Trim()))
        End Sub

        Private Async Function DoModerationActionAsync(action As Func(Of Task(Of Boolean))) As Task
            Try
                Me.UseWaitCursor = True
                SetActionsEnabled(False)
                Await action()
                Await LoadReportsAsync()
                Dim parent = TryCast(Me.MdiParent, MainForm)
                If parent IsNot Nothing Then Await parent.RefreshPendingBadgeAsync()
            Catch ex As Exception
                MessageBox.Show("Echec : " & ex.Message, "Signalements",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Sub CopyIds_Click(sender As Object, e As EventArgs)
            Dim r = Selected()
            If r Is Nothing Then Return

            Clipboard.SetText(
                "report_id=" & r.Id & vbCrLf &
                "direct_message_id=" & r.DirectMessageId & vbCrLf &
                "reporter_id=" & r.ReporterId & vbCrLf &
                "reported_user_id=" & r.ReportedUserId)
        End Sub

        Private Class StatusItem
            Public ReadOnly Property Label As String
            Public ReadOnly Property Value As String

            Public Sub New(label As String, value As String)
                Me.Label = label
                Me.Value = value
            End Sub

            Public Overrides Function ToString() As String
                Return Label
            End Function
        End Class

    End Class

End Namespace
