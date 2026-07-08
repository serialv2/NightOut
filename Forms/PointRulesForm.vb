Option Strict Off
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    Public Class PointRulesForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly grid As New DataGridView()
        Private ReadOnly btnRefresh As New Button()
        Private ReadOnly btnSave As New Button()
        Private ReadOnly lblInfo As New Label()
        Private _rules As BindingList(Of PointRule)

        Public Sub New()
            Me.Text = "Règles de points"
            Me.BackColor = NightOutTheme.BgDark
            Me.ForeColor = NightOutTheme.Cream
            Me.Font = NightOutTheme.FontBody(9.5F)
            Me.Padding = New Padding(14)

            lblInfo.Dock = DockStyle.Top
            lblInfo.Height = 42
            lblInfo.ForeColor = NightOutTheme.Muted
            lblInfo.Text = "Modifie ici les gains et dépenses de points utilisés par l'application NightOut. Une règle inactive ne donne plus de points."

            Dim panelTop As New FlowLayoutPanel() With {
                .Dock = DockStyle.Top,
                .Height = 44,
                .FlowDirection = FlowDirection.LeftToRight,
                .BackColor = NightOutTheme.BgDark
            }

            btnRefresh.Text = "↻ Recharger"
            btnRefresh.Width = 130
            btnRefresh.Height = 32
            btnSave.Text = "💾 Enregistrer la ligne sélectionnée"
            btnSave.Width = 260
            btnSave.Height = 32
            panelTop.Controls.Add(btnRefresh)
            panelTop.Controls.Add(btnSave)

            grid.Dock = DockStyle.Fill
            grid.AutoGenerateColumns = False
            grid.AllowUserToAddRows = False
            grid.AllowUserToDeleteRows = False
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            grid.MultiSelect = False
            grid.BackgroundColor = NightOutTheme.BgPanel
            grid.BorderStyle = BorderStyle.None
            grid.RowHeadersVisible = False
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

            ApplyGridStyle()

            grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "RuleKey", .HeaderText = "Clé", .ReadOnly = True, .FillWeight = 150})
            grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "Label", .HeaderText = "Libellé", .FillWeight = 170})
            grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "Description", .HeaderText = "Description", .FillWeight = 260})
            grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "Amount", .HeaderText = "Points", .FillWeight = 70})
            grid.Columns.Add(New DataGridViewCheckBoxColumn() With {.DataPropertyName = "IsActive", .HeaderText = "Actif", .FillWeight = 60})
            grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "SortOrder", .HeaderText = "Ordre", .FillWeight = 60})

            Me.Controls.Add(grid)
            Me.Controls.Add(panelTop)
            Me.Controls.Add(lblInfo)

            AddHandler btnRefresh.Click, Async Sub() Await RefreshDataAsync()
            AddHandler btnSave.Click, Async Sub() Await SaveSelectedAsync()
        End Sub


        Private Sub ApplyGridStyle()
            grid.EnableHeadersVisualStyles = False
            grid.BackgroundColor = Color.FromArgb(15, 23, 32)
            grid.GridColor = Color.FromArgb(210, 215, 225)
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            grid.RowTemplate.Height = 30

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 35, 50)
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
            grid.ColumnHeadersDefaultCellStyle.Font = New Font(Me.Font, FontStyle.Bold)
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 35, 50)
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White

            grid.DefaultCellStyle.BackColor = Color.White
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(25, 25, 25)
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215)
            grid.DefaultCellStyle.SelectionForeColor = Color.White
            grid.DefaultCellStyle.Padding = New Padding(4, 0, 4, 0)

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(242, 245, 250)
            grid.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(25, 25, 25)
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215)
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White

            grid.RowsDefaultCellStyle.BackColor = Color.White
            grid.RowsDefaultCellStyle.ForeColor = Color.FromArgb(25, 25, 25)
            grid.RowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215)
            grid.RowsDefaultCellStyle.SelectionForeColor = Color.White
        End Sub
        Protected Overrides Async Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Cursor = Cursors.WaitCursor
                Dim list = Await PointRuleService.GetAllAsync()
                _rules = New BindingList(Of PointRule)(list)
                grid.DataSource = _rules
            Catch ex As Exception
                MessageBox.Show("Impossible de charger les règles de points :" & Environment.NewLine & ex.Message,
                                "NightOut Admin", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Cursor = Cursors.Default
            End Try
        End Function

        Private Async Function SaveSelectedAsync() As Task
            If grid.CurrentRow Is Nothing OrElse grid.CurrentRow.DataBoundItem Is Nothing Then Return
            Dim rule = DirectCast(grid.CurrentRow.DataBoundItem, PointRule)

            Try
                Cursor = Cursors.WaitCursor
                Dim ok = Await PointRuleService.UpdateAsync(rule)
                If ok Then
                    MessageBox.Show("Règle enregistrée.", "NightOut Admin", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Await RefreshDataAsync()
                End If
            Catch ex As Exception
                MessageBox.Show("Impossible d'enregistrer la règle :" & Environment.NewLine & ex.Message,
                                "NightOut Admin", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Cursor = Cursors.Default
            End Try
        End Function

    End Class

End Namespace
