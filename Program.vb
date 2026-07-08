Option Strict Off
Option Explicit On

Imports System.Windows.Forms
Imports NightOutAdmin.Forms

Namespace Global.NightOutAdmin

    Friend Module Program

        <STAThread>
        Sub Main()
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)

            ' 1) Connexion admin obligatoire
            Using login As New LoginForm()
                If login.ShowDialog() <> DialogResult.OK Then
                    Return ' utilisateur a annulé / échec
                End If
            End Using

            ' 2) Fenêtre MDI principale
            Application.Run(New MainForm())
        End Sub

    End Module

End Namespace
