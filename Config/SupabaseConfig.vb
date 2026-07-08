Option Strict Off
Option Explicit On

Namespace Config

    ''' <summary>
    ''' Configuration Supabase NightOut (projet keeraqtoiwvcybhavkfb).
    ''' La clé "anon" est la même que celle utilisée par l'app MAUI.
    ''' Les droits d'écriture admin reposent sur les politiques RLS
    ''' (profiles.is_admin = true) appliquées au JWT de l'utilisateur connecté.
    ''' </summary>
    Public Module SupabaseConfig

        Public Const Url As String = "https://keeraqtoiwvcybhavkfb.supabase.co"

        Public Const AnonKey As String =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." &
            "eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImtlZXJhcXRvaXd2Y3liaGF2a2ZiIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzU4MjIwNjgsImV4cCI6MjA5MTM5ODA2OH0." &
            "K6B0AvqZfKNhpH3dxB8sc9LzOlRX_rIb64CdfTl5vUo"

        ' Endpoints
        Public ReadOnly Property RestUrl As String
            Get
                Return Url & "/rest/v1/"
            End Get
        End Property

        Public ReadOnly Property AuthUrl As String
            Get
                Return Url & "/auth/v1/"
            End Get
        End Property

    End Module

End Namespace
