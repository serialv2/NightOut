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

    ''' <summary>Validation des bars en attente (status = pending).</summary>
    Public Class BarsValidationForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly grid As New DataGridView()
        Private ReadOnly pnlDetail As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly lblName As New Label()
        Private ReadOnly lblInfo As New Label()
        Private ReadOnly lblDesc As New Label()
        Private ReadOnly btnApprove As New Button()
        Private ReadOnly btnReject As New Button()
        Private ReadOnly lblEmpty As New Label()
        Private _bars As New List(Of Bar)()

        Public Sub New()
            Me.Text = "Bars à valider"
            Me.BackColor = NightOutTheme.BgDark

            lblTitle.Text = "⏳  Bars en attente de validation"
            lblTitle.ForeColor = NightOutTheme.Orange
            lblTitle.Font = NightOutTheme.FontTitle(13.0F)
            lblTitle.Dock = DockStyle.Top
            lblTitle.Height = 46
            lblTitle.TextAlign = ContentAlignment.MiddleLeft
            lblTitle.Padding = New Padding(14, 0, 0, 0)
            lblTitle.BackColor = NightOutTheme.BgPanel

            ' Détail à droite
            pnlDetail.Dock = DockStyle.Right
            pnlDetail.Width = 360
            pnlDetail.BackColor = NightOutTheme.BgPanel
            pnlDetail.Padding = New Padding(18)

            lblName.Text = "Sélectionnez un bar"
            lblName.ForeColor = NightOutTheme.Gold
            lblName.Font = NightOutTheme.FontTitle(14.0F)
            lblName.Dock = DockStyle.Top
            lblName.Height = 54

            lblInfo.ForeColor = NightOutTheme.Cream
            lblInfo.Font = NightOutTheme.FontBody(9.5F)
            lblInfo.Dock = DockStyle.Top
            lblInfo.Height = 150

            lblDesc.ForeColor = NightOutTheme.Muted
            lblDesc.Font = NightOutTheme.FontBody(9.0F)
            lblDesc.Dock = DockStyle.Top
            lblDesc.Height = 140

            NightOutTheme.StylePrimaryButton(btnApprove, NightOutTheme.Green)
            btnApprove.Text = "✓  Valider le bar"
            btnApprove.Dock = DockStyle.Bottom
            btnApprove.Height = 44
            btnApprove.Enabled = False

            NightOutTheme.StyleGhostButton(btnReject, NightOutTheme.Red)
            btnReject.Text = "✕  Refuser"
            btnReject.Dock = DockStyle.Bottom
            btnReject.Height = 40
            btnReject.Enabled = False

            pnlDetail.Controls.Add(lblDesc)
            pnlDetail.Controls.Add(lblInfo)
            pnlDetail.Controls.Add(lblName)
            Dim spacer As New Panel() With {.Dock = DockStyle.Bottom, .Height = 10, .BackColor = NightOutTheme.BgPanel}
            pnlDetail.Controls.Add(btnApprove)
            pnlDetail.Controls.Add(spacer)
            pnlDetail.Controls.Add(btnReject)

            ' Grille
            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.Columns.Add(NewCol("Name", "Nom"))
            grid.Columns.Add(NewCol("Address", "Adresse"))
            grid.Columns.Add(NewCol("City", "Ville"))
            grid.Columns.Add(NewCol("Created", "Demandé le"))
            grid.Columns("Created").FillWeight = 60

            lblEmpty.Text = "🎉 Aucun bar en attente"
            lblEmpty.ForeColor = NightOutTheme.Muted
            lblEmpty.Font = NightOutTheme.FontTitle(13.0F)
            lblEmpty.Dock = DockStyle.Fill
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter
            lblEmpty.Visible = False

            Dim host As New Panel() With {.Dock = DockStyle.Fill}
            host.Controls.Add(grid)
            host.Controls.Add(lblEmpty)

            Me.Controls.Add(host)
            Me.Controls.Add(pnlDetail)
            Me.Controls.Add(lblTitle)

            AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
            AddHandler grid.CellClick, AddressOf Grid_CellClick
            AddHandler grid.CellEnter, AddressOf Grid_CellEnter
            AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
            AddHandler btnApprove.Click, AddressOf Approve_Click
            AddHandler btnReject.Click, AddressOf Reject_Click
        End Sub

        Private Shared Function NewCol(name As String, header As String) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header}
        End Function

        Private Async Sub BarsValidationForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Me.UseWaitCursor = True
                _bars = Await BarService.GetByStatusAsync("pending")
                grid.Rows.Clear()
                For Each b In _bars
                    grid.Rows.Add(b.Name, b.Address, If(b.AddressCityName, "—"),
                                  b.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"))
                Next
                lblEmpty.Visible = (_bars.Count = 0)
                grid.Visible = (_bars.Count > 0)
                If _bars.Count > 0 AndAlso grid.Rows.Count > 0 Then
                    grid.ClearSelection()
                    grid.CurrentCell = grid.Rows(0).Cells(0)
                    grid.Rows(0).Selected = True
                    ShowBar(_bars(0))
                Else
                    ClearDetail()
                End If
            Catch ex As Exception
                MessageBox.Show("Erreur de chargement : " & ex.Message, "Bars",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Sub ClearDetail()
            lblName.Text = "Sélectionnez un bar"
            lblInfo.Text = ""
            lblDesc.Text = ""
            btnApprove.Enabled = False
            btnReject.Enabled = False
        End Sub

        Private Function Selected() As Bar
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim idx = grid.CurrentRow.Index
            If idx < 0 OrElse idx >= _bars.Count Then Return Nothing
            Return _bars(idx)
        End Function

        Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
            Dim b = Selected()
            If b Is Nothing Then ClearDetail() : Return
            ShowBar(b)
        End Sub

        Private Sub Grid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 OrElse e.RowIndex >= _bars.Count Then Return
            grid.CurrentCell = grid.Rows(e.RowIndex).Cells(0)
            grid.Rows(e.RowIndex).Selected = True
            ShowBar(_bars(e.RowIndex))
        End Sub

        Private Sub Grid_CellEnter(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 OrElse e.RowIndex >= _bars.Count Then Return
            If e.ColumnIndex > 0 Then grid.CurrentCell = grid.Rows(e.RowIndex).Cells(0)
            ShowBar(_bars(e.RowIndex))
        End Sub

        Private Sub ShowBar(b As Bar)
            If b Is Nothing Then ClearDetail() : Return
            lblName.Text = b.Name
            lblInfo.Text =
                $"📍 {b.Address}" & vbCrLf & vbCrLf &
                $"🏷 Catégorie : {If(String.IsNullOrEmpty(b.Category), "—", b.Category)}" & vbCrLf &
                $"📞 {If(String.IsNullOrEmpty(b.Phone), "—", b.Phone)}" & vbCrLf &
                $"🌐 {If(String.IsNullOrEmpty(b.Website), "—", b.Website)}" & vbCrLf &
                $"📷 {If(String.IsNullOrEmpty(b.Instagram), "—", b.Instagram)}" & vbCrLf &
                $"🗺 {b.Latitude:0.0000}, {b.Longitude:0.0000}"
            lblDesc.Text = If(String.IsNullOrWhiteSpace(b.Description), "(Aucune description)", b.Description)
            btnApprove.Enabled = True
            btnReject.Enabled = True
        End Sub

        Private Async Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim b = Selected()
            If b Is Nothing Then Return
            Using f As New BarEditForm(b)
                If f.ShowDialog(Me) = DialogResult.OK Then
                    Await RefreshDataAsync()
                    Dim parent = TryCast(Me.MdiParent, MainForm)
                    If parent IsNot Nothing Then Await parent.RefreshPendingBadgeAsync()
                End If
            End Using
        End Sub

        Private Async Sub Approve_Click(sender As Object, e As EventArgs)
            Dim b = Selected()
            If b Is Nothing Then Return
            Await DoAction(Function() BarService.ApproveAsync(b.Id), $"« {b.Name} » validé.")
        End Sub

        Private Async Sub Reject_Click(sender As Object, e As EventArgs)
            Dim b = Selected()
            If b Is Nothing Then Return
            If MessageBox.Show($"Refuser « {b.Name} » ?", "Confirmation",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            Await DoAction(Function() BarService.RejectAsync(b.Id), $"« {b.Name} » refusé.")
        End Sub

        Private Async Function DoAction(action As Func(Of Task(Of Boolean)), okMsg As String) As Task
            Try
                btnApprove.Enabled = False : btnReject.Enabled = False
                Me.UseWaitCursor = True
                Await action()
                Await RefreshDataAsync()
                Dim parent = TryCast(Me.MdiParent, MainForm)
                If parent IsNot Nothing Then Await parent.RefreshPendingBadgeAsync()
            Catch ex As Exception
                MessageBox.Show("Échec : " & ex.Message, "Action",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

    End Class

End Namespace
