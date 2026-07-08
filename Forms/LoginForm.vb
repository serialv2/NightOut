Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    Partial Class LoginForm

        Public Sub New()
            InitializeComponent()
            ApplyTheme()
        End Sub

        Private Sub ApplyTheme()
            Me.BackColor = NightOutTheme.BgDark
            pnlCard.BackColor = NightOutTheme.BgPanel

            lblLogo.ForeColor = NightOutTheme.Gold
            lblLogo.Font = New Font("Segoe UI", 22.0F, FontStyle.Bold Or FontStyle.Italic)

            lblSub.ForeColor = NightOutTheme.Muted
            lblSub.Font = NightOutTheme.FontBody(9.5F)

            For Each lbl In {lblEmail, lblPassword}
                lbl.ForeColor = NightOutTheme.Muted
                lbl.Font = NightOutTheme.FontBody(8.5F)
            Next

            For Each tb In {txtEmail, txtPassword}
                tb.BackColor = NightOutTheme.BgPanel2
                tb.ForeColor = NightOutTheme.Cream
                tb.Font = NightOutTheme.FontBody(10.5F)
            Next

            NightOutTheme.StylePrimaryButton(btnLogin)
            lblError.ForeColor = NightOutTheme.Red
            lblError.Font = NightOutTheme.FontBody(8.5F)
        End Sub

        Private Async Sub btnLogin_Click(sender As Object, e As EventArgs) Handles btnLogin.Click
            lblError.Text = ""

            Dim email = txtEmail.Text.Trim()
            Dim pwd = txtPassword.Text

            If String.IsNullOrWhiteSpace(email) OrElse String.IsNullOrWhiteSpace(pwd) Then
                lblError.Text = "Veuillez renseigner email et mot de passe."
                Return
            End If

            btnLogin.Enabled = False
            btnLogin.Text = "Connexion…"
            Me.UseWaitCursor = True

            Try
                Dim res = Await AuthService.LoginAsAdminAsync(email, pwd)
                If res.Ok Then
                    Me.DialogResult = DialogResult.OK
                    Me.Close()
                Else
                    lblError.Text = res.Message
                End If
            Catch ex As Exception
                lblError.Text = "Erreur : " & ex.Message
            Finally
                btnLogin.Enabled = True
                btnLogin.Text = "Se connecter"
                Me.UseWaitCursor = False
            End Try
        End Sub

    End Class

End Namespace
