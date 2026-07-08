Option Strict On
Option Explicit On

Namespace Models

    Public Class BeaconScanItem
        Public Property BluetoothAddress As ULong
        Public Property AddressText As String = ""
        Public Property Name As String = ""
        Public Property Rssi As Integer
        Public Property LastSeenUtc As DateTime = DateTime.UtcNow
        Public Property IsFeasycom As Boolean
        Public Property LocalName As String = ""
        Public Property ManufacturerHex As String = ""

        Public Overrides Function ToString() As String
            Dim badge = If(IsFeasycom, "⭐ ", "")
            Dim displayName = If(String.IsNullOrWhiteSpace(Name), "Beacon BLE", Name)
            Return $"{badge}{displayName}  ·  {AddressText}  ·  RSSI {Rssi} dBm"
        End Function
    End Class

    Public Class BeaconProgrammingProfile
        Public Property BarId As String = ""
        Public Property BarName As String = ""
        Public Property DeviceName As String = "SPOTIZ-BEACON"
        Public Property Pin As String = "000000"
        Public Property NewPin As String = "000000"
        Public Property Uuid As String = "FDA50693-A4E2-4FB1-AFCF-C6EB07647825"
        Public Property Major As Integer
        Public Property Minor As Integer
        Public Property BroadcastIntervalMs As Integer = 1000
        Public Property DeviceTxPower As Integer = 0
        Public Property BeaconTxPower As Integer = 2
        Public Property BluetoothAddress As String = ""
    End Class


    Public Class BeaconReadConfiguration
        Public Property DeviceName As String = ""
        Public Property Pin As String = ""
        Public Property Uuid As String = ""
        Public Property Major As Integer?
        Public Property Minor As Integer?
        Public Property BroadcastIntervalMs As Integer?
        Public Property DeviceTxPower As Integer?
        Public Property TlmEnabled As Boolean?
        Public Property RawIBeaconPayload As String = ""
        Public Property RawText As String = ""
    End Class

    Public Class BeaconProgrammingResult
        Public Property Success As Boolean
        Public Property Message As String = ""
        Public Property Commands As New List(Of String)()
        Public Property Responses As New List(Of String)()
    End Class

    Public Class BarLookupItem
        Public Property Id As String = ""
        Public Property Name As String = ""

        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

End Namespace
