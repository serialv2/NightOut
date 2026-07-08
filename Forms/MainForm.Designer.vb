Option Strict Off
Option Explicit On

Imports System.Windows.Forms
Imports System.Drawing

Namespace Forms

    Partial Class MainForm
        Inherits Form

        Private components As System.ComponentModel.IContainer

        Friend WithEvents menuMain As MenuStrip
        Friend WithEvents miAccueil As ToolStripMenuItem
        Friend WithEvents miBars As ToolStripMenuItem
        Friend WithEvents miBarsValider As ToolStripMenuItem
        Friend WithEvents miBarsGestion As ToolStripMenuItem
        Friend WithEvents miBarClaims As ToolStripMenuItem
        Friend WithEvents miEvenements As ToolStripMenuItem
        Friend WithEvents miPro As ToolStripMenuItem
        Friend WithEvents miUtilisateurs As ToolStripMenuItem
        Friend WithEvents miStats As ToolStripMenuItem
        Friend WithEvents miFenetres As ToolStripMenuItem
        Friend WithEvents miCascade As ToolStripMenuItem
        Friend WithEvents miTileH As ToolStripMenuItem
        Friend WithEvents miTileV As ToolStripMenuItem
        Friend WithEvents miCompte As ToolStripMenuItem
        Friend WithEvents miProprietes As ToolStripMenuItem
        Friend WithEvents miDeconnexion As ToolStripMenuItem

        Friend WithEvents toolBar As ToolStrip
        Friend WithEvents tbAccueil As ToolStripButton
        Friend WithEvents tbValider As ToolStripButton
        Friend WithEvents tbBars As ToolStripButton
        Friend WithEvents tbEvents As ToolStripButton
        Friend WithEvents tbPro As ToolStripButton
        Friend WithEvents tbUsers As ToolStripButton
        Friend WithEvents tbStats As ToolStripButton
        Friend WithEvents tbProprietes As ToolStripButton
        Friend WithEvents tbSep1 As ToolStripSeparator
        Friend WithEvents tbRefresh As ToolStripButton

        Friend WithEvents statusBar As StatusStrip
        Friend WithEvents lblStatusUser As ToolStripStatusLabel
        Friend WithEvents lblStatusSpring As ToolStripStatusLabel
        Friend WithEvents lblStatusPending As ToolStripStatusLabel

        Protected Overrides Sub Dispose(disposing As Boolean)
            Try
                If disposing AndAlso components IsNot Nothing Then components.Dispose()
            Finally
                MyBase.Dispose(disposing)
            End Try
        End Sub

        Private Sub InitializeComponent()
            Me.menuMain = New MenuStrip()
            Me.miAccueil = New ToolStripMenuItem()
            Me.miBars = New ToolStripMenuItem()
            Me.miBarsValider = New ToolStripMenuItem()
            Me.miBarsGestion = New ToolStripMenuItem()
            Me.miBarClaims = New ToolStripMenuItem()
            Me.miEvenements = New ToolStripMenuItem()
            Me.miPro = New ToolStripMenuItem()
            Me.miUtilisateurs = New ToolStripMenuItem()
            Me.miStats = New ToolStripMenuItem()
            Me.miFenetres = New ToolStripMenuItem()
            Me.miCascade = New ToolStripMenuItem()
            Me.miTileH = New ToolStripMenuItem()
            Me.miTileV = New ToolStripMenuItem()
            Me.miCompte = New ToolStripMenuItem()
            Me.miProprietes = New ToolStripMenuItem()
            Me.miDeconnexion = New ToolStripMenuItem()

            Me.toolBar = New ToolStrip()
            Me.tbAccueil = New ToolStripButton()
            Me.tbValider = New ToolStripButton()
            Me.tbBars = New ToolStripButton()
            Me.tbEvents = New ToolStripButton()
            Me.tbPro = New ToolStripButton()
            Me.tbUsers = New ToolStripButton()
            Me.tbStats = New ToolStripButton()
            Me.tbProprietes = New ToolStripButton()
            Me.tbSep1 = New ToolStripSeparator()
            Me.tbRefresh = New ToolStripButton()

            Me.statusBar = New StatusStrip()
            Me.lblStatusUser = New ToolStripStatusLabel()
            Me.lblStatusSpring = New ToolStripStatusLabel()
            Me.lblStatusPending = New ToolStripStatusLabel()

            Me.menuMain.SuspendLayout()
            Me.toolBar.SuspendLayout()
            Me.statusBar.SuspendLayout()
            Me.SuspendLayout()
            '
            ' menuMain
            '
            Me.menuMain.Items.AddRange(New ToolStripItem() {
                Me.miAccueil, Me.miBars, Me.miEvenements, Me.miPro,
                Me.miUtilisateurs, Me.miStats, Me.miFenetres, Me.miCompte})
            Me.menuMain.Location = New Point(0, 0)
            Me.menuMain.Dock = DockStyle.Top
            Me.menuMain.Text = "menuMain"
            '
            Me.miAccueil.Text = "🗺  Accueil"
            Me.miBars.Text = "🍺  Bars"
            Me.miBarsValider.Text = "⏳  Bars à valider"
            Me.miBarsGestion.Text = "📋  Gestion des bars"
            Me.miBarClaims.Text = "🔐  Demandes de récupération"
            Me.miBars.DropDownItems.AddRange(New ToolStripItem() {Me.miBarsValider, Me.miBarsGestion, Me.miBarClaims})
            Me.miEvenements.Text = "🎉  Événements"
            Me.miPro.Text = "🏢  Comptes pro"
            Me.miUtilisateurs.Text = "👥  Utilisateurs"
            Me.miStats.Text = "📊  Statistiques"
            Me.miFenetres.Text = "🪟  Fenêtres"
            Me.miCascade.Text = "Cascade"
            Me.miTileH.Text = "Mosaïque horizontale"
            Me.miTileV.Text = "Mosaïque verticale"
            Me.miFenetres.DropDownItems.AddRange(New ToolStripItem() {Me.miCascade, Me.miTileH, Me.miTileV})
            Me.miCompte.Text = "⚙  Compte"
            Me.miProprietes.Text = "Proprietes"
            Me.miDeconnexion.Text = "Se déconnecter"
            Me.miCompte.DropDownItems.AddRange(New ToolStripItem() {Me.miProprietes, New ToolStripSeparator(), Me.miDeconnexion})
            '
            ' toolBar
            '
            Me.toolBar.Items.AddRange(New ToolStripItem() {
                Me.tbAccueil, Me.tbValider, Me.tbBars, Me.tbEvents,
                Me.tbPro, Me.tbUsers, Me.tbStats, Me.tbProprietes, Me.tbSep1, Me.tbRefresh})
            Me.toolBar.Location = New Point(0, 24)
            Me.toolBar.Dock = DockStyle.Top
            Me.toolBar.GripStyle = ToolStripGripStyle.Hidden
            Me.toolBar.ImageScalingSize = New Size(20, 20)
            Me.tbAccueil.Text = "Accueil"
            Me.tbAccueil.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbValider.Text = "À valider"
            Me.tbValider.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbBars.Text = "Bars"
            Me.tbBars.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbEvents.Text = "Événements"
            Me.tbEvents.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbPro.Text = "Comptes pro"
            Me.tbPro.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbUsers.Text = "Utilisateurs"
            Me.tbUsers.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbStats.Text = "Statistiques"
            Me.tbStats.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbProprietes.Text = "Proprietes"
            Me.tbProprietes.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbRefresh.Text = "↻ Rafraîchir"
            Me.tbRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text
            Me.tbRefresh.Alignment = ToolStripItemAlignment.Right
            '
            ' statusBar
            '
            Me.statusBar.Items.AddRange(New ToolStripItem() {
                Me.lblStatusUser, Me.lblStatusSpring, Me.lblStatusPending})
            Me.statusBar.Dock = DockStyle.Bottom
            Me.lblStatusUser.Text = "Connecté"
            Me.lblStatusSpring.Spring = True
            Me.lblStatusSpring.Text = ""
            Me.lblStatusPending.Text = ""
            '
            ' MainForm
            '
            Me.AutoScaleMode = AutoScaleMode.None
            Me.ClientSize = New Size(1180, 760)
            Me.Controls.Add(Me.toolBar)
            Me.Controls.Add(Me.statusBar)
            Me.Controls.Add(Me.menuMain)
            Me.IsMdiContainer = True
            Me.MainMenuStrip = Me.menuMain
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.Text = "NightOut — Console d'administration"
            Me.WindowState = FormWindowState.Maximized
            Me.menuMain.ResumeLayout(False)
            Me.toolBar.ResumeLayout(False)
            Me.statusBar.ResumeLayout(False)
            Me.ResumeLayout(False)
            Me.PerformLayout()
        End Sub

    End Class

End Namespace
