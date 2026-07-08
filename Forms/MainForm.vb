Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    Partial Class MainForm

        Private miPointsRulesDynamic As ToolStripMenuItem
        Private tbPointsRulesDynamic As ToolStripButton
        Private miReportsDynamic As ToolStripMenuItem
        Private tbReportsDynamic As ToolStripButton
        Private miBeaconsDynamic As ToolStripMenuItem
        Private tbBeaconsDynamic As ToolStripButton
        Public Sub New()
            InitializeComponent()
            EnsurePropertiesNavigation()
            AddPointRulesNavigation()
            AddReportsNavigation()
            AddBeaconsNavigation()
            ApplyTheme()
        End Sub

        Private Sub AddPointRulesNavigation()
            miPointsRulesDynamic = New ToolStripMenuItem("⭐  Règles de points")
            AddHandler miPointsRulesDynamic.Click, Sub() OpenChild(Of PointRulesForm)()
            menuMain.Items.Insert(Math.Max(0, menuMain.Items.IndexOf(miFenetres)), miPointsRulesDynamic)

            tbPointsRulesDynamic = New ToolStripButton("Points") With {
                .DisplayStyle = ToolStripItemDisplayStyle.Text
            }
            AddHandler tbPointsRulesDynamic.Click, Sub() OpenChild(Of PointRulesForm)()
            toolBar.Items.Insert(Math.Max(0, toolBar.Items.IndexOf(tbSep1)), tbPointsRulesDynamic)
        End Sub

        Private Sub AddReportsNavigation()
            miReportsDynamic = New ToolStripMenuItem("Signalements")
            AddHandler miReportsDynamic.Click, Sub() OpenChild(Of MessageReportsForm)()
            menuMain.Items.Insert(Math.Max(0, menuMain.Items.IndexOf(miFenetres)), miReportsDynamic)

            tbReportsDynamic = New ToolStripButton("Signalements") With {
                .DisplayStyle = ToolStripItemDisplayStyle.Text
            }
            AddHandler tbReportsDynamic.Click, Sub() OpenChild(Of MessageReportsForm)()
            toolBar.Items.Insert(Math.Max(0, toolBar.Items.IndexOf(tbSep1)), tbReportsDynamic)
        End Sub


        Private Sub AddBeaconsNavigation()
            miBeaconsDynamic = New ToolStripMenuItem("📡  Beacons Spotiz")
            AddHandler miBeaconsDynamic.Click, Sub() OpenChild(Of BeaconProgrammingForm)()
            menuMain.Items.Insert(Math.Max(0, menuMain.Items.IndexOf(miFenetres)), miBeaconsDynamic)

            tbBeaconsDynamic = New ToolStripButton("Beacons") With {
                .DisplayStyle = ToolStripItemDisplayStyle.Text
            }
            AddHandler tbBeaconsDynamic.Click, Sub() OpenChild(Of BeaconProgrammingForm)()
            toolBar.Items.Insert(Math.Max(0, toolBar.Items.IndexOf(tbSep1)), tbBeaconsDynamic)
        End Sub

        Private Sub OpenProprietes() Handles miProprietes.Click, tbProprietes.Click
            OpenChild(Of AppSettingsForm)()
        End Sub

        Private Sub EnsurePropertiesNavigation()
            miProprietes.Text = "Proprietes"
            miProprietes.Visible = True
            tbProprietes.Text = "Proprietes"
            tbProprietes.Visible = True

            If Not miCompte.DropDownItems.Contains(miProprietes) Then
                miCompte.DropDownItems.Insert(0, miProprietes)
            End If

            If Not toolBar.Items.Contains(tbProprietes) Then
                Dim insertIndex = toolBar.Items.IndexOf(tbSep1)
                If insertIndex < 0 Then insertIndex = toolBar.Items.Count
                toolBar.Items.Insert(insertIndex, tbProprietes)
            End If
        End Sub

        Private Sub ApplyTheme()
            Me.BackColor = NightOutTheme.BgDark

            menuMain.BackColor = NightOutTheme.BgPanel
            menuMain.ForeColor = NightOutTheme.Cream
            menuMain.Renderer = New DarkMenuRenderer()
            menuMain.Font = NightOutTheme.FontBody(9.5F)

            toolBar.BackColor = NightOutTheme.BgPanel2
            toolBar.ForeColor = NightOutTheme.Cream
            toolBar.Renderer = New DarkMenuRenderer()
            For Each it As ToolStripItem In toolBar.Items
                it.ForeColor = NightOutTheme.Cream
            Next
            tbValider.ForeColor = NightOutTheme.Orange
            If tbReportsDynamic IsNot Nothing Then tbReportsDynamic.ForeColor = NightOutTheme.Red
            If tbBeaconsDynamic IsNot Nothing Then tbBeaconsDynamic.ForeColor = NightOutTheme.Gold
            tbProprietes.ForeColor = NightOutTheme.Cream

            statusBar.BackColor = NightOutTheme.BgPanel
            statusBar.ForeColor = NightOutTheme.Muted
            lblStatusUser.ForeColor = NightOutTheme.Gold
        End Sub

        Private Async Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Dim name = If(AuthService.CurrentProfile?.NameDisplay, "Admin")
            lblStatusUser.Text = $"👤 {name} (admin)"

            ' Ouvre la carte d'accueil par défaut
            OpenChild(Of MapHomeForm)()

            Await RefreshPendingBadgeAsync()
        End Sub

        ''' <summary>Ouvre (ou ramène au premier plan) un MDI child unique par type.</summary>
        Private Function OpenChild(Of T As {Form, New})() As T
            For Each f As Form In Me.MdiChildren
                If TypeOf f Is T Then
                    f.Activate()
                    Return DirectCast(f, T)
                End If
            Next
            Dim child As New T()
            child.MdiParent = Me
            child.WindowState = FormWindowState.Maximized
            child.Show()
            Return child
        End Function

        Public Async Function RefreshPendingBadgeAsync() As Task
            Try
                Dim barsPending = Await SupabaseClient.CountAsync("bars", "status=eq.pending")
                Dim proPending = Await SupabaseClient.CountAsync("profiles", "professional_status=eq.pending")
                Dim reportsPending = Await SupabaseClient.CountAsync("message_reports", "status=eq.pending")
                lblStatusPending.Text = $"{barsPending} bar(s) · {proPending} patron(s) · {reportsPending} signalement(s) en attente"
                lblStatusPending.ForeColor = If(barsPending + proPending + reportsPending > 0, NightOutTheme.Orange, NightOutTheme.Muted)
            Catch
                lblStatusPending.Text = ""
            End Try
        End Function

        ' ── Navigation (menu + toolbar) ──
        Private Sub OpenAccueil() Handles miAccueil.Click, tbAccueil.Click
            OpenChild(Of MapHomeForm)()
        End Sub

        Private Sub OpenValider() Handles miBarsValider.Click, tbValider.Click
            OpenChild(Of BarsValidationForm)()
        End Sub

        Private Sub OpenBarsGestion() Handles miBarsGestion.Click, tbBars.Click
            OpenChild(Of BarsManageForm)()
        End Sub

        Private Sub OpenEvents() Handles miEvenements.Click, tbEvents.Click
            OpenChild(Of EventsForm)()
        End Sub

        Private Sub OpenPro() Handles miPro.Click, tbPro.Click
            OpenChild(Of ProAccountsForm)()
        End Sub

        Private Sub OpenUsers() Handles miUtilisateurs.Click, tbUsers.Click
            OpenChild(Of UsersForm)()
        End Sub

        Private Sub OpenStats() Handles miStats.Click, tbStats.Click
            OpenChild(Of StatsForm)()
        End Sub

        ' Permet à la carte d'accueil de demander l'ouverture de la validation
        Public Sub NavigateToValidation()
            OpenChild(Of BarsValidationForm)()
        End Sub

        ' ── Fenêtres ──
        Private Sub Cascade() Handles miCascade.Click
            Me.LayoutMdi(MdiLayout.Cascade)
        End Sub
        Private Sub TileH() Handles miTileH.Click
            Me.LayoutMdi(MdiLayout.TileHorizontal)
        End Sub
        Private Sub TileV() Handles miTileV.Click
            Me.LayoutMdi(MdiLayout.TileVertical)
        End Sub

        ' ── Rafraîchir le child actif ──
        Private Async Sub RefreshActiveChild() Handles tbRefresh.Click
            Await RefreshPendingBadgeAsync()
            Dim active = Me.ActiveMdiChild
            If active IsNot Nothing AndAlso TypeOf active Is IRefreshable Then
                Await DirectCast(active, IRefreshable).RefreshDataAsync()
            End If
        End Sub

        ' ── Déconnexion ──
        Private Sub Logout() Handles miDeconnexion.Click
            If MessageBox.Show("Se déconnecter ?", "NightOut Admin",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                AuthService.Logout()
                Application.Restart()
            End If
        End Sub

    End Class

    ''' <summary>Contrat optionnel pour rafraîchir un MDI child.</summary>
    Public Interface IRefreshable
        Function RefreshDataAsync() As Task
    End Interface

    ''' <summary>Renderer sombre pour MenuStrip/ToolStrip.</summary>
    Public Class DarkMenuRenderer
        Inherits ToolStripProfessionalRenderer

        Public Sub New()
            MyBase.New(New DarkColorTable())
        End Sub

        Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
            If Not e.Item.Selected Then
                If e.Item.ForeColor = SystemColors.ControlText OrElse e.Item.ForeColor = Color.Empty Then
                    e.TextColor = NightOutTheme.Cream
                Else
                    e.TextColor = e.Item.ForeColor
                End If
            Else
                e.TextColor = NightOutTheme.Gold
            End If
            MyBase.OnRenderItemText(e)
        End Sub
    End Class

    Public Class DarkColorTable
        Inherits ProfessionalColorTable

        Public Overrides ReadOnly Property MenuItemSelected As Color
            Get
                Return NightOutTheme.BgPanel3
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemSelectedGradientBegin As Color
            Get
                Return NightOutTheme.BgPanel3
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemSelectedGradientEnd As Color
            Get
                Return NightOutTheme.BgPanel3
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemBorder As Color
            Get
                Return NightOutTheme.Border
            End Get
        End Property
        Public Overrides ReadOnly Property ButtonSelectedHighlight As Color
            Get
                Return NightOutTheme.BgPanel3
            End Get
        End Property
        Public Overrides ReadOnly Property ButtonSelectedBorder As Color
            Get
                Return NightOutTheme.Border
            End Get
        End Property
        Public Overrides ReadOnly Property ToolStripDropDownBackground As Color
            Get
                Return NightOutTheme.BgPanel
            End Get
        End Property
        Public Overrides ReadOnly Property ImageMarginGradientBegin As Color
            Get
                Return NightOutTheme.BgPanel
            End Get
        End Property
        Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color
            Get
                Return NightOutTheme.BgPanel
            End Get
        End Property
        Public Overrides ReadOnly Property ImageMarginGradientEnd As Color
            Get
                Return NightOutTheme.BgPanel
            End Get
        End Property
        Public Overrides ReadOnly Property MenuBorder As Color
            Get
                Return NightOutTheme.Border
            End Get
        End Property
    End Class

End Namespace
