Option Strict On
Option Explicit On

Imports System.Globalization
Imports NightOutAdmin.Models

Namespace Services

    ''' <summary>
    ''' Génère les commandes AT Feasycom identifiées dans le SDK Android 3.3.6.
    ''' Le SDK Android encapsule ces commandes avec FscBeaconApiImp : AT+NAME, AT+LENAME, AT+PIN,
    ''' AT+ADVIN, AT+TXPOWER, AT+BADVPARM, AT+BADVDATA, AT+TLM, AT+ADVEXT, etc.
    ''' </summary>
    Public NotInheritable Class FeasyBeaconCommandBuilder

        Private Sub New()
        End Sub

        Public Shared Function BuildSpotizIBeaconCommands(profile As BeaconProgrammingProfile) As List(Of String)
            Validate(profile)

            Dim uuidNoDash = NormalizeUuid(profile.Uuid)
            Dim majorHex = UInt16ToHex(profile.Major)
            Dim minorHex = UInt16ToHex(profile.Minor)
            Dim measuredPowerHex = SignedByteToHex(-59) ' Valeur iBeacon standard de départ, ajustable plus tard après calibration terrain.
            Dim fullIBeaconPayload = BuildIBeaconAdvertisingPayload(uuidNoDash, majorHex, minorHex, measuredPowerHex)

            Dim commands As New List(Of String) From {
                $"AT+NAME={profile.DeviceName}",
                $"AT+LENAME={profile.DeviceName}",
                $"AT+ADVIN={profile.BroadcastIntervalMs}",
                $"AT+TXPOWER={profile.DeviceTxPower}",
                "AT+TLM=0"
            }

            ' Feasycom BP104D : AT+BADVDATA attend le paquet advertising complet en HEX.
            ' Le beacon lu en usine renvoie par exemple :
            ' +BADVDATA=,0,0201061AFF4C000215{UUID}{MAJOR}{MINOR}{POWER},1
            ' Donc on programme le slot 0, qui est le vrai slot iBeacon primaire scanné par Android/iOS.
            commands.Add($"AT+BADVDATA=0,{fullIBeaconPayload},1")

            If Not String.IsNullOrWhiteSpace(profile.NewPin) Then
                commands.Add($"AT+PIN={profile.NewPin}")
            End If

            Return commands
        End Function

        Public Shared Function BuildReadConfigurationCommands() As List(Of String)
            Return New List(Of String) From {
                "AT+NAME",
                "AT+PIN",
                "AT+ADVIN",
                "AT+TXPOWER",
                "AT+BADVDATA",
                "AT+TLM"
            }
        End Function

        Private Shared Sub Validate(profile As BeaconProgrammingProfile)
            If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))
            If String.IsNullOrWhiteSpace(profile.DeviceName) Then Throw New ArgumentException("Le nom du beacon est obligatoire.")
            If profile.DeviceName.Length > 18 Then Throw New ArgumentException("Le nom du beacon doit rester court (18 caractères maximum recommandé BLE).")
            If profile.Major < 0 OrElse profile.Major > 65535 Then Throw New ArgumentException("Major doit être compris entre 0 et 65535.")
            If profile.Minor < 0 OrElse profile.Minor > 65535 Then Throw New ArgumentException("Minor doit être compris entre 0 et 65535.")
            If profile.BroadcastIntervalMs < 100 OrElse profile.BroadcastIntervalMs > 10000 Then Throw New ArgumentException("Intervalle conseillé : 100 à 10000 ms.")
            NormalizeUuid(profile.Uuid)
        End Sub

        Public Shared Function NormalizeUuid(uuid As String) As String
            Dim g As Guid
            If Not Guid.TryParse(uuid, g) Then Throw New ArgumentException("UUID iBeacon invalide.")
            Return g.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant()
        End Function

        Private Shared Function UInt16ToHex(value As Integer) As String
            Return value.ToString("X4", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function BuildIBeaconAdvertisingPayload(uuidNoDash As String, majorHex As String, minorHex As String, measuredPowerHex As String) As String
            ' 02 01 06 = Flags
            ' 1A FF 4C 00 02 15 = Apple iBeacon manufacturer frame
            Return "0201061AFF4C000215" & uuidNoDash & majorHex & minorHex & measuredPowerHex
        End Function

        Private Shared Function SignedByteToHex(value As Integer) As String
            Dim b = CByte(value And &HFF)
            Return b.ToString("X2", CultureInfo.InvariantCulture)
        End Function

    End Class

End Namespace
