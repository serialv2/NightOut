Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    ''' <summary>Gestion des utilisateurs : recherche, vérification, droits admin.</summary>
    Public Class UsersForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly pnlTop As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly txtSearch As New TextBox()
        Private ReadOnly btnSearch As New Button()
        Private ReadOnly grid As New DataGridView()

        Private ReadOnly pnlActions As New Panel()
        Private ReadOnly btnActivity As New Button()
        Private ReadOnly btnVerify As New Button()
        Private ReadOnly btnAdmin As New Button()

        Private _view As New List(Of Profile)()

        Public Sub New()
            Me.Text = "Utilisateurs"
            Me.BackColor = NightOutTheme.BgDark

            pnlTop.Dock = DockStyle.Top
            pnlTop.Height = 92
            pnlTop.BackColor = NightOutTheme.BgPanel

            lblTitle.Text = "👥  Utilisateurs"
            lblTitle.ForeColor = NightOutTheme.Gold
            lblTitle.Font = NightOutTheme.FontTitle(13.0F)
            lblTitle.AutoSize = True
            lblTitle.Location = New Point(14, 12)
            pnlTop.Controls.Add(lblTitle)

            txtSearch.Location = New Point(14, 50)
            txtSearch.Size = New Size(320, 28)
            txtSearch.BackColor = NightOutTheme.BgPanel2
            txtSearch.ForeColor = NightOutTheme.Cream
            txtSearch.BorderStyle = BorderStyle.FixedSingle
            txtSearch.Font = NightOutTheme.FontBody(10.0F)
            pnlTop.Controls.Add(txtSearch)

            NightOutTheme.StylePrimaryButton(btnSearch)
            btnSearch.Text = "Rechercher"
            btnSearch.Location = New Point(344, 49)
            btnSearch.Size = New Size(120, 30)
            pnlTop.Controls.Add(btnSearch)

            pnlActions.Dock = DockStyle.Bottom
            pnlActions.Height = 56
            pnlActions.BackColor = NightOutTheme.BgPanel
            pnlActions.Padding = New Padding(14, 10, 14, 10)

            NightOutTheme.StylePrimaryButton(btnActivity, NightOutTheme.Gold)
            btnActivity.Text = "Voir activité"
            btnActivity.Width = 160
            btnActivity.Dock = DockStyle.Left
            btnActivity.Enabled = False

            Dim sp0 As New Panel With {.Width = 10, .Dock = DockStyle.Left}

            NightOutTheme.StyleGhostButton(btnVerify, NightOutTheme.Blue)
            btnVerify.Text = "✔  Vérifié On / Off"
            btnVerify.Width = 200
            btnVerify.Dock = DockStyle.Left
            btnVerify.Enabled = False

            Dim sp As New Panel With {.Width = 10, .Dock = DockStyle.Left}

            NightOutTheme.StyleGhostButton(btnAdmin, NightOutTheme.Gold)
            btnAdmin.Text = "🛡  Admin On / Off"
            btnAdmin.Width = 200
            btnAdmin.Dock = DockStyle.Left
            btnAdmin.Enabled = False

            pnlActions.Controls.Add(btnAdmin)
            pnlActions.Controls.Add(sp)
            pnlActions.Controls.Add(btnVerify)
            pnlActions.Controls.Add(sp0)
            pnlActions.Controls.Add(btnActivity)

            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.Columns.Add(NewCol("Username", "Pseudo"))
            grid.Columns.Add(NewCol("Display", "Nom affiché"))
            grid.Columns.Add(NewCol("Type", "Type"))
            grid.Columns.Add(NewCol("Admin", "Admin"))
            grid.Columns.Add(NewCol("Verified", "Vérifié"))
            grid.Columns.Add(NewCol("Created", "Inscrit le"))
            grid.Columns("Admin").FillWeight = 40
            grid.Columns("Verified").FillWeight = 45
            grid.Columns("Type").FillWeight = 50

            Me.Controls.Add(grid)
            Me.Controls.Add(pnlActions)
            Me.Controls.Add(pnlTop)

            AddHandler btnSearch.Click, AddressOf Search_Click
            AddHandler txtSearch.KeyDown, AddressOf Search_KeyDown
            AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
            AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
            AddHandler btnActivity.Click, AddressOf Activity_Click
            AddHandler btnVerify.Click, AddressOf Verify_Click
            AddHandler btnAdmin.Click, AddressOf Admin_Click
        End Sub

        Private Shared Function NewCol(name As String, header As String) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header}
        End Function

        Private Async Sub UsersForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await LoadUsers(Nothing)
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Await LoadUsers(If(String.IsNullOrWhiteSpace(txtSearch.Text), Nothing, txtSearch.Text.Trim()))
        End Function

        Private Async Function LoadUsers(search As String) As Task
            Try
                Me.UseWaitCursor = True
                _view = Await UserService.GetAllAsync(search)
                grid.Rows.Clear()
                For Each p In _view
                    grid.Rows.Add(p.Username, If(p.DisplayName, "—"),
                                  If(p.AccountType, "user"),
                                  If(p.IsAdmin, "🛡", "—"),
                                  If(p.IsVerified, "✔", "—"),
                                  p.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"))
                Next
                lblTitle.Text = $"👥  Utilisateurs ({_view.Count})"
            Catch ex As Exception
                MessageBox.Show("Erreur : " & ex.Message, "Utilisateurs", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Async Sub Search_Click(sender As Object, e As EventArgs)
            Await LoadUsers(If(String.IsNullOrWhiteSpace(txtSearch.Text), Nothing, txtSearch.Text.Trim()))
        End Sub

        Private Async Sub Search_KeyDown(sender As Object, e As KeyEventArgs)
            If e.KeyCode = Keys.Enter Then
                e.SuppressKeyPress = True
                Await LoadUsers(If(String.IsNullOrWhiteSpace(txtSearch.Text), Nothing, txtSearch.Text.Trim()))
            End If
        End Sub

        Private Function Selected() As Profile
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim idx = grid.CurrentRow.Index
            If idx < 0 OrElse idx >= _view.Count Then Return Nothing
            Return _view(idx)
        End Function

        Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
            Dim has = (Selected() IsNot Nothing)
            btnActivity.Enabled = has
            btnVerify.Enabled = has
            btnAdmin.Enabled = has
        End Sub

        Private Sub Activity_Click(sender As Object, e As EventArgs)
            OpenActivity()
        End Sub

        Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            grid.CurrentCell = grid.Rows(e.RowIndex).Cells(0)
            OpenActivity()
        End Sub

        Private Sub OpenActivity()
            Dim p = Selected()
            If p Is Nothing Then Return

            Using f As New UserActivityForm(p)
                f.ShowDialog(Me)
            End Using
        End Sub

        Private Async Sub Verify_Click(sender As Object, e As EventArgs)
            Dim p = Selected() : If p Is Nothing Then Return
            Await DoAction(Function() UserService.SetVerifiedAsync(p.Id, Not p.IsVerified))
        End Sub

        Private Async Sub Admin_Click(sender As Object, e As EventArgs)
            Dim p = Selected() : If p Is Nothing Then Return
            Dim verb = If(p.IsAdmin, "retirer les droits admin de", "donner les droits admin à")
            If MessageBox.Show($"Voulez-vous {verb} « {p.NameDisplay} » ?", "Droits admin",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            Await DoAction(Function() UserService.SetAdminAsync(p.Id, Not p.IsAdmin))
        End Sub

        Private Async Function DoAction(action As Func(Of Task(Of Boolean))) As Task
            Try
                Me.UseWaitCursor = True
                Await action()
                Await RefreshDataAsync()
            Catch ex As Exception
                MessageBox.Show("Échec : " & ex.Message, "Action", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

    End Class

End Namespace
