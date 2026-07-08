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

    Public Class UserActivityForm
        Inherits Form

        Private ReadOnly _user As Profile
        Private ReadOnly pnlHeader As New Panel()
        Private ReadOnly lblTitle As New Label()
        Private ReadOnly lblSubtitle As New Label()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly tabs As New TabControl()

        Public Sub New(user As Profile)
            _user = user

            Me.Text = "Activité utilisateur"
            Me.BackColor = NightOutTheme.BgDark
            Me.ForeColor = NightOutTheme.Cream
            Me.StartPosition = FormStartPosition.CenterParent
            Me.Size = New Size(1180, 760)
            Me.MinimumSize = New Size(900, 560)

            pnlHeader.Dock = DockStyle.Top
            pnlHeader.Height = 112
            pnlHeader.BackColor = NightOutTheme.BgPanel
            pnlHeader.Padding = New Padding(18, 14, 18, 10)

            lblTitle.AutoSize = True
            lblTitle.ForeColor = NightOutTheme.Gold
            lblTitle.Font = NightOutTheme.FontTitle(16.0F)
            lblTitle.Location = New Point(18, 14)
            lblTitle.Text = "Activité de " & If(_user?.NameDisplay, "utilisateur")

            lblSubtitle.AutoSize = True
            lblSubtitle.ForeColor = NightOutTheme.Muted
            lblSubtitle.Font = NightOutTheme.FontBody(10.0F)
            lblSubtitle.Location = New Point(20, 48)
            lblSubtitle.Text = $"{If(_user?.Username, "—")} · {If(_user?.AccountType, "user")} · ID {_user?.Id}"

            lblStatus.AutoSize = True
            lblStatus.ForeColor = NightOutTheme.Cream
            lblStatus.Font = NightOutTheme.FontBody(9.5F)
            lblStatus.Location = New Point(20, 76)
            lblStatus.Text = "Chargement de l'activité..."

            pnlHeader.Controls.Add(lblTitle)
            pnlHeader.Controls.Add(lblSubtitle)
            pnlHeader.Controls.Add(lblStatus)

            tabs.Dock = DockStyle.Fill
            tabs.Font = NightOutTheme.FontBody(9.0F)
            tabs.Appearance = TabAppearance.Normal

            Me.Controls.Add(tabs)
            Me.Controls.Add(pnlHeader)

            AddHandler Me.Shown, AddressOf UserActivityForm_Shown
        End Sub

        Private Async Sub UserActivityForm_Shown(sender As Object, e As EventArgs)
            Await LoadActivityAsync()
        End Sub

        Private Async Function LoadActivityAsync() As Task
            Try
                Me.UseWaitCursor = True
                tabs.TabPages.Clear()

                Dim data = Await UserActivityService.GetAsync(_user)
                For Each section In data.Sections
                    AddSection(section)
                Next

                Dim totalRows = data.Sections.Sum(Function(s) s.Rows.Count)
                lblStatus.Text = $"{data.Sections.Count} section(s) · {totalRows} ligne(s) d'activité"
                If data.Errors.Count > 0 Then
                    lblStatus.Text &= $" · {data.Errors.Count} section(s) partielle(s)"
                    lblStatus.ForeColor = NightOutTheme.Orange
                Else
                    lblStatus.ForeColor = NightOutTheme.Green
                End If
            Catch ex As Exception
                lblStatus.Text = "Erreur pendant le chargement."
                lblStatus.ForeColor = NightOutTheme.Red
                MessageBox.Show("Impossible de charger l'activité : " & ex.Message, "Activité utilisateur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Sub AddSection(section As UserActivitySection)
            Dim page As New TabPage($"{section.Title} ({section.Rows.Count})")
            page.BackColor = NightOutTheme.BgDark
            page.ForeColor = NightOutTheme.Cream
            page.Padding = New Padding(8)

            Dim grid As New DataGridView()
            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True

            For Each col In section.Columns
                grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = col, .HeaderText = col})
            Next

            If section.Rows.Count = 0 Then
                grid.Rows.Add(Enumerable.Range(0, section.Columns.Length).Select(Function(i) If(i = 0, "Aucune donnée", "")).ToArray())
            Else
                For Each row In section.Rows
                    grid.Rows.Add(row)
                Next
            End If

            If grid.Columns.Count > 0 Then
                grid.Columns(grid.Columns.Count - 1).FillWeight = 180
            End If

            page.Controls.Add(grid)
            tabs.TabPages.Add(page)
        End Sub

    End Class

End Namespace
