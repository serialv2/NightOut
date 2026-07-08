Option Strict Off
Option Explicit On

Imports System.Windows.Forms
Imports System.Drawing

Namespace Forms

    Partial Class LoginForm
        Inherits Form

        Private components As System.ComponentModel.IContainer

        Friend WithEvents pnlCard As Panel
        Friend WithEvents lblLogo As Label
        Friend WithEvents lblSub As Label
        Friend WithEvents txtEmail As TextBox
        Friend WithEvents txtPassword As TextBox
        Friend WithEvents lblEmail As Label
        Friend WithEvents lblPassword As Label
        Friend WithEvents btnLogin As Button
        Friend WithEvents lblError As Label

        Protected Overrides Sub Dispose(disposing As Boolean)
            Try
                If disposing AndAlso components IsNot Nothing Then components.Dispose()
            Finally
                MyBase.Dispose(disposing)
            End Try
        End Sub

        Private Sub InitializeComponent()
            Me.pnlCard = New Panel()
            Me.lblLogo = New Label()
            Me.lblSub = New Label()
            Me.lblEmail = New Label()
            Me.txtEmail = New TextBox()
            Me.lblPassword = New Label()
            Me.txtPassword = New TextBox()
            Me.btnLogin = New Button()
            Me.lblError = New Label()
            Me.SuspendLayout()
            '
            ' pnlCard
            '
            Me.pnlCard.Location = New Point(70, 60)
            Me.pnlCard.Size = New Size(320, 320)
            Me.pnlCard.Controls.Add(Me.lblLogo)
            Me.pnlCard.Controls.Add(Me.lblSub)
            Me.pnlCard.Controls.Add(Me.lblEmail)
            Me.pnlCard.Controls.Add(Me.txtEmail)
            Me.pnlCard.Controls.Add(Me.lblPassword)
            Me.pnlCard.Controls.Add(Me.txtPassword)
            Me.pnlCard.Controls.Add(Me.btnLogin)
            Me.pnlCard.Controls.Add(Me.lblError)
            '
            ' lblLogo
            '
            Me.lblLogo.AutoSize = False
            Me.lblLogo.Location = New Point(0, 18)
            Me.lblLogo.Size = New Size(320, 34)
            Me.lblLogo.Text = "NightOut"
            Me.lblLogo.TextAlign = ContentAlignment.MiddleCenter
            '
            ' lblSub
            '
            Me.lblSub.AutoSize = False
            Me.lblSub.Location = New Point(0, 52)
            Me.lblSub.Size = New Size(320, 20)
            Me.lblSub.Text = "Console d'administration"
            Me.lblSub.TextAlign = ContentAlignment.MiddleCenter
            '
            ' lblEmail
            '
            Me.lblEmail.Location = New Point(28, 92)
            Me.lblEmail.Size = New Size(264, 18)
            Me.lblEmail.Text = "Email administrateur"
            '
            ' txtEmail
            '
            Me.txtEmail.Location = New Point(28, 112)
            Me.txtEmail.Size = New Size(264, 26)
            Me.txtEmail.BorderStyle = BorderStyle.FixedSingle
            '
            ' lblPassword
            '
            Me.lblPassword.Location = New Point(28, 150)
            Me.lblPassword.Size = New Size(264, 18)
            Me.lblPassword.Text = "Mot de passe"
            '
            ' txtPassword
            '
            Me.txtPassword.Location = New Point(28, 170)
            Me.txtPassword.Size = New Size(264, 26)
            Me.txtPassword.BorderStyle = BorderStyle.FixedSingle
            Me.txtPassword.UseSystemPasswordChar = True
            '
            ' btnLogin
            '
            Me.btnLogin.Location = New Point(28, 214)
            Me.btnLogin.Size = New Size(264, 40)
            Me.btnLogin.Text = "Se connecter"
            '
            ' lblError
            '
            Me.lblError.Location = New Point(28, 262)
            Me.lblError.Size = New Size(264, 44)
            Me.lblError.Text = ""
            Me.lblError.TextAlign = ContentAlignment.TopCenter
            '
            ' LoginForm
            '
            Me.AutoScaleMode = AutoScaleMode.None
            Me.ClientSize = New Size(460, 440)
            Me.Controls.Add(Me.pnlCard)
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.Text = "NightOut Admin — Connexion"
            Me.AcceptButton = Me.btnLogin
            Me.ResumeLayout(False)
        End Sub

    End Class

End Namespace
