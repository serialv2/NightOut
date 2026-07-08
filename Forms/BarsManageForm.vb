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

    ''' <summary>Gestion globale des bars : recherche, filtre, actions.</summary>
    Public Class BarsManageForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly pnlTop As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly txtSearch As New TextBox()
        Private ReadOnly cboStatus As New ComboBox()
        Private ReadOnly grid As New DataGridView()

        Private ReadOnly pnlActions As New Panel()
        Private ReadOnly btnDetails As New Button()
        Private ReadOnly btnEdit As New Button()
        Private ReadOnly btnToggleActive As New Button()
        Private ReadOnly btnTogglePremium As New Button()
        Private ReadOnly btnDelete As New Button()

        Private _all As New List(Of Bar)()
        Private _view As New List(Of Bar)()

        Public Sub New()
            Me.Text = "Gestion des bars"
            Me.BackColor = NightOutTheme.BgDark

            ' Barre haute
            pnlTop.Dock = DockStyle.Top
            pnlTop.Height = 92
            pnlTop.BackColor = NightOutTheme.BgPanel

            lblTitle.Text = "🍺  Tous les établissements"
            lblTitle.ForeColor = NightOutTheme.Gold
            lblTitle.Font = NightOutTheme.FontTitle(13.0F)
            lblTitle.AutoSize = True
            lblTitle.Location = New Point(14, 12)
            pnlTop.Controls.Add(lblTitle)

            txtSearch.Location = New Point(14, 50)
            txtSearch.Size = New Size(280, 28)
            txtSearch.BackColor = NightOutTheme.BgPanel2
            txtSearch.ForeColor = NightOutTheme.Cream
            txtSearch.BorderStyle = BorderStyle.FixedSingle
            txtSearch.Font = NightOutTheme.FontBody(10.0F)
            pnlTop.Controls.Add(txtSearch)

            cboStatus.Location = New Point(306, 50)
            cboStatus.Size = New Size(180, 28)
            cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
            cboStatus.FlatStyle = FlatStyle.Flat
            cboStatus.BackColor = NightOutTheme.BgPanel2
            cboStatus.ForeColor = NightOutTheme.Cream
            cboStatus.Items.AddRange(New Object() {"Tous les statuts", "Validés", "En attente", "Refusés"})
            cboStatus.SelectedIndex = 0
            pnlTop.Controls.Add(cboStatus)

            ' Actions
            pnlActions.Dock = DockStyle.Bottom
            pnlActions.Height = 56
            pnlActions.BackColor = NightOutTheme.BgPanel
            pnlActions.Padding = New Padding(14, 10, 14, 10)

            NightOutTheme.StylePrimaryButton(btnDetails, NightOutTheme.Gold)
            btnDetails.Text = "Voir détails"
            btnDetails.Width = 150
            btnDetails.Dock = DockStyle.Left
            btnDetails.Enabled = False

            Dim spDetails As New Panel With {.Width = 10, .Dock = DockStyle.Left}

            NightOutTheme.StylePrimaryButton(btnEdit, NightOutTheme.Gold)
            btnEdit.Text = "✏  Modifier la fiche"
            btnEdit.Width = 180
            btnEdit.Dock = DockStyle.Left
            btnEdit.Enabled = False

            Dim sp0 As New Panel With {.Width = 10, .Dock = DockStyle.Left}

            NightOutTheme.StyleGhostButton(btnToggleActive, NightOutTheme.Gold)
            btnToggleActive.Text = "Activer / Désactiver"
            btnToggleActive.Width = 190
            btnToggleActive.Dock = DockStyle.Left
            btnToggleActive.Enabled = False

            Dim sp1 As New Panel With {.Width = 10, .Dock = DockStyle.Left}

            NightOutTheme.StyleGhostButton(btnTogglePremium, NightOutTheme.Orange)
            btnTogglePremium.Text = "Premium On / Off"
            btnTogglePremium.Width = 170
            btnTogglePremium.Dock = DockStyle.Left
            btnTogglePremium.Enabled = False

            NightOutTheme.StyleGhostButton(btnDelete, NightOutTheme.Red)
            btnDelete.Text = "🗑  Supprimer"
            btnDelete.Width = 150
            btnDelete.Dock = DockStyle.Right
            btnDelete.Enabled = False

            ' Les contrôles Dock=Left s'empilent dans l'ordre inverse d'ajout :
            ' on ajoute donc le plus à droite en premier.
            pnlActions.Controls.Add(btnTogglePremium)
            pnlActions.Controls.Add(sp1)
            pnlActions.Controls.Add(btnToggleActive)
            pnlActions.Controls.Add(sp0)
            pnlActions.Controls.Add(btnEdit)
            pnlActions.Controls.Add(spDetails)
            pnlActions.Controls.Add(btnDetails)
            pnlActions.Controls.Add(btnDelete)

            ' Grille
            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.Columns.Add(NewCol("Name", "Nom"))
            grid.Columns.Add(NewCol("City", "Ville"))
            grid.Columns.Add(NewCol("Cat", "Catégorie"))
            grid.Columns.Add(NewCol("Status", "Statut"))
            grid.Columns.Add(NewCol("Active", "Actif"))
            grid.Columns.Add(NewCol("Premium", "Premium"))
            grid.Columns("Status").FillWeight = 60
            grid.Columns("Active").FillWeight = 40
            grid.Columns("Premium").FillWeight = 45

            Me.Controls.Add(grid)
            Me.Controls.Add(pnlActions)
            Me.Controls.Add(pnlTop)

            AddHandler txtSearch.TextChanged, AddressOf Filter_Changed
            AddHandler cboStatus.SelectedIndexChanged, AddressOf Filter_Changed
            AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
            AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
            AddHandler btnDetails.Click, AddressOf Details_Click
            AddHandler btnEdit.Click, AddressOf Edit_Click
            AddHandler btnToggleActive.Click, AddressOf ToggleActive_Click
            AddHandler btnTogglePremium.Click, AddressOf TogglePremium_Click
            AddHandler btnDelete.Click, AddressOf Delete_Click
        End Sub

        Private Shared Function NewCol(name As String, header As String) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header}
        End Function

        Private Async Sub BarsManageForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Me.UseWaitCursor = True
                _all = Await BarService.GetAllAsync()
                ApplyFilter()
            Catch ex As Exception
                MessageBox.Show("Erreur : " & ex.Message, "Bars", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Sub Filter_Changed(sender As Object, e As EventArgs)
            ApplyFilter()
        End Sub

        Private Sub ApplyFilter()
            Dim q = txtSearch.Text.Trim().ToLowerInvariant()
            Dim statusFilter As String = Nothing
            Select Case cboStatus.SelectedIndex
                Case 1 : statusFilter = "approved"
                Case 2 : statusFilter = "pending"
                Case 3 : statusFilter = "rejected"
            End Select

            Dim filtered = _all.AsEnumerable()
            If statusFilter IsNot Nothing Then
                filtered = filtered.Where(Function(b) b.Status = statusFilter)
            End If
            If Not String.IsNullOrEmpty(q) Then
                filtered = filtered.Where(Function(b) _
                    (b.Name IsNot Nothing AndAlso b.Name.ToLowerInvariant().Contains(q)) OrElse
                    (b.Address IsNot Nothing AndAlso b.Address.ToLowerInvariant().Contains(q)))
            End If

            _view = filtered.ToList()
            grid.Rows.Clear()
            For Each b In _view
                grid.Rows.Add(b.Name, If(b.AddressCityName, "—"),
                              If(String.IsNullOrEmpty(b.Category), "—", b.Category),
                              b.StatusLabel,
                              If(b.IsActive, "Oui", "Non"),
                              If(b.IsPremium, "★", "—"))
            Next
            lblTitle.Text = $"🍺  Tous les établissements ({_view.Count})"
        End Sub

        Private Function Selected() As Bar
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim idx = grid.CurrentRow.Index
            If idx < 0 OrElse idx >= _view.Count Then Return Nothing
            Return _view(idx)
        End Function

        Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
            Dim has = (Selected() IsNot Nothing)
            btnDetails.Enabled = has
            btnEdit.Enabled = has
            btnToggleActive.Enabled = has
            btnTogglePremium.Enabled = has
            btnDelete.Enabled = has
        End Sub

        Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            grid.CurrentCell = grid.Rows(e.RowIndex).Cells(0)
            OpenDetails()
        End Sub

        Private Sub Details_Click(sender As Object, e As EventArgs)
            OpenDetails()
        End Sub

        Private Sub OpenDetails()
            Dim b = Selected()
            If b Is Nothing Then Return
            Using f As New BarStatsDetailForm(b)
                If f.ShowDialog(Me) = DialogResult.OK Then
                    Dim t = RefreshDataAsync()
                End If
            End Using
        End Sub

        Private Sub Edit_Click(sender As Object, e As EventArgs)
            OpenEditor()
        End Sub

        Private Sub OpenEditor()
            Dim b = Selected()
            If b Is Nothing Then Return
            Using f As New BarEditForm(b)
                If f.ShowDialog(Me) = DialogResult.OK Then
                    Dim t = RefreshDataAsync()
                End If
            End Using
        End Sub

        Private Async Sub ToggleActive_Click(sender As Object, e As EventArgs)
            Dim b = Selected() : If b Is Nothing Then Return
            Await DoAction(Function() BarService.SetActiveAsync(b.Id, Not b.IsActive))
        End Sub

        Private Async Sub TogglePremium_Click(sender As Object, e As EventArgs)
            Dim b = Selected() : If b Is Nothing Then Return
            Await DoAction(Function() BarService.SetPremiumAsync(b.Id, Not b.IsPremium))
        End Sub

        Private Async Sub Delete_Click(sender As Object, e As EventArgs)
            Dim b = Selected() : If b Is Nothing Then Return
            If MessageBox.Show($"Supprimer définitivement « {b.Name} » ?" & vbCrLf &
                "Cette action est irréversible.", "Suppression",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
            Await DoAction(Function() BarService.DeleteAsync(b.Id))
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
