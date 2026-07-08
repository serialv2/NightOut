Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    Public Class AppSettingsForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly lblInfo As New Label()
        Private ReadOnly lblCheckinDuration As New Label()
        Private ReadOnly numCheckinDuration As New NumericUpDown()
        Private ReadOnly btnRefresh As New Button()
        Private ReadOnly btnSave As New Button()

        Public Sub New()
            Me.Text = "Parametres application"
            Me.BackColor = NightOutTheme.BgDark
            Me.ForeColor = NightOutTheme.Cream
            Me.Font = NightOutTheme.FontBody(9.5F)
            Me.Padding = New Padding(18)

            BuildUi()

            AddHandler btnRefresh.Click, Async Sub() Await RefreshDataAsync()
            AddHandler btnSave.Click, Async Sub() Await SaveAsync()
        End Sub

        Private Sub BuildUi()
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .ColumnCount = 2,
                .RowCount = 4,
                .AutoSize = True,
                .BackColor = NightOutTheme.BgDark
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 260))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 320))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))

            lblInfo.Text = "Reglages globaux utilises par l'application mobile."
            lblInfo.Dock = DockStyle.Fill
            lblInfo.ForeColor = NightOutTheme.Muted
            lblInfo.TextAlign = ContentAlignment.MiddleLeft
            root.Controls.Add(lblInfo, 0, 0)
            root.SetColumnSpan(lblInfo, 2)

            lblCheckinDuration.Text = "Duree presence check-in"
            lblCheckinDuration.Dock = DockStyle.Fill
            lblCheckinDuration.ForeColor = NightOutTheme.Cream
            lblCheckinDuration.TextAlign = ContentAlignment.MiddleLeft
            root.Controls.Add(lblCheckinDuration, 0, 1)

            numCheckinDuration.Minimum = 5
            numCheckinDuration.Maximum = 1440
            numCheckinDuration.Value = 60
            numCheckinDuration.Increment = 5
            numCheckinDuration.Width = 120
            numCheckinDuration.BackColor = NightOutTheme.BgPanel
            numCheckinDuration.ForeColor = NightOutTheme.Cream
            root.Controls.Add(numCheckinDuration, 1, 1)

            Dim help As New Label With {
                .Text = "En minutes. Exemple : 60 = l'utilisateur reste present 1 heure sans check-out manuel.",
                .Dock = DockStyle.Fill,
                .ForeColor = NightOutTheme.Muted,
                .TextAlign = ContentAlignment.MiddleLeft
            }
            root.Controls.Add(help, 1, 2)

            Dim actions As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.LeftToRight,
                .BackColor = NightOutTheme.BgDark
            }

            btnRefresh.Text = "Recharger"
            btnRefresh.Width = 120
            btnRefresh.Height = 32
            btnRefresh.BackColor = NightOutTheme.BgPanel2
            btnRefresh.ForeColor = NightOutTheme.Cream

            btnSave.Text = "Enregistrer"
            btnSave.Width = 140
            btnSave.Height = 32
            btnSave.BackColor = NightOutTheme.Gold
            btnSave.ForeColor = Color.Black

            actions.Controls.Add(btnRefresh)
            actions.Controls.Add(btnSave)
            root.Controls.Add(actions, 1, 3)

            Me.Controls.Add(root)
        End Sub

        Protected Overrides Async Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Cursor = Cursors.WaitCursor
                Dim minutes = Await AppSettingService.GetCheckinPresenceMinutesAsync()
                numCheckinDuration.Value = Math.Max(numCheckinDuration.Minimum, Math.Min(numCheckinDuration.Maximum, minutes))
            Catch ex As Exception
                MessageBox.Show("Impossible de charger les parametres :" & Environment.NewLine & ex.Message,
                                "NightOut Admin", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Cursor = Cursors.Default
            End Try
        End Function

        Private Async Function SaveAsync() As Task
            Try
                Cursor = Cursors.WaitCursor
                Await AppSettingService.SaveCheckinPresenceMinutesAsync(CInt(numCheckinDuration.Value))
                MessageBox.Show("Parametre enregistre.", "NightOut Admin", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show("Impossible d'enregistrer le parametre :" & Environment.NewLine & ex.Message,
                                "NightOut Admin", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Cursor = Cursors.Default
            End Try
        End Function

    End Class

End Namespace
