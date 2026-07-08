Option Strict Off
Option Explicit On

Imports System.Threading.Tasks
Imports NightOutAdmin.Models

Namespace Services

    Public Module AuthService

        Public Property CurrentProfile As Profile

        ''' <summary>
        ''' Connexion + contrôle is_admin. Retourne (ok, message).
        ''' Refuse l'accès si le profil n'a pas is_admin = true.
        ''' </summary>
        Public Async Function LoginAsAdminAsync(email As String, password As String) _
            As Task(Of (Ok As Boolean, Message As String))

            Dim res = Await SupabaseClient.SignInAsync(email, password)
            If Not res.Ok Then
                Return (False, res.Message)
            End If

            ' Charger le profil de l'utilisateur connecté
            Try
                Dim profiles = Await SupabaseClient.GetListAsync(Of Profile)(
                    $"profiles?id=eq.{res.UserId}&select=*&limit=1")

                If profiles Is Nothing OrElse profiles.Count = 0 Then
                    SupabaseClient.SignOut()
                    Return (False, "Profil introuvable.")
                End If

                Dim p = profiles(0)
                If Not p.IsAdmin Then
                    SupabaseClient.SignOut()
                    Return (False, "Accès refusé : ce compte n'est pas administrateur.")
                End If

                CurrentProfile = p
                Return (True, "Connecté")

            Catch ex As Exception
                SupabaseClient.SignOut()
                Return (False, "Erreur lors du chargement du profil : " & ex.Message)
            End Try
        End Function

        Public Sub Logout()
            CurrentProfile = Nothing
            SupabaseClient.SignOut()
        End Sub

    End Module

End Namespace
