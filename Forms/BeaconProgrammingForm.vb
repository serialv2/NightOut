Option Strict On
Option Explicit On

Imports System.Threading
Imports System.Collections
Imports System.Security.Cryptography
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    Public Class BeaconProgrammingForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly _ble As New FeasyBeaconBleService()
        Private ReadOnly _devices As New BindingSource()
        Private ReadOnly _bars As New BindingSource()
        Private _selectedDevice As BeaconScanItem
        Private _cts As CancellationTokenSource

        Private WithEvents btnScan As Button
        Private WithEvents btnStop As Button
        Private WithEvents btnConnect As Button
        Private WithEvents btnRead As Button
        Private WithEvents btnProgram As Button
        Private WithEvents btnGeneratePin As Button
        Private WithEvents btnSaveSupabase As Button
        Private lstDevices As ListBox
        Private cboBars As ComboBox
        Private txtName As TextBox
        Private txtPin As TextBox
        Private txtNewPin As TextBox
        Private txtUuid As TextBox
        Private numMajor As NumericUpDown
        Private numMinor As NumericUpDown
        Private numInterval As NumericUpDown
        Private numDeviceTx As NumericUpDown
        Private numBeaconTx As NumericUpDown
        Private txtLog As TextBox
        Private lblState As Label

        Public Sub New()
            Me.Text = "Programmation Beacons Spotiz"
            Me.BackColor = NightOutTheme.BgDark
            Me.ForeColor = NightOutTheme.Cream
            Me.Font = NightOutTheme.FontBody(9.0F)
            BuildUi()
            AddHandler _ble.DeviceFound, AddressOf OnDeviceFound
            AddHandler _ble.Log, AddressOf AppendLogSafe
            AddHandler _ble.ResponseReceived, Sub(r) AppendLogSafe("Réponse brute : " & r)
        End Sub

        Private Sub BuildUi()
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .Padding = New Padding(14),
                .BackColor = NightOutTheme.BgDark
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
            Me.Controls.Add(root)

            Dim left As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 4, .ColumnCount = 1, .BackColor = NightOutTheme.BgDark}
            left.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            left.RowStyles.Add(New RowStyle(SizeType.Percent, 50))
            left.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            left.RowStyles.Add(New RowStyle(SizeType.Percent, 50))
            root.Controls.Add(left, 0, 0)

            Dim scanBar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .BackColor = NightOutTheme.BgDark}
            btnScan = MakeButton("Scanner BLE")
            btnStop = MakeButton("Stop")
            btnConnect = MakeButton("Connecter")
            scanBar.Controls.AddRange({btnScan, btnStop, btnConnect})
            left.Controls.Add(scanBar, 0, 0)

            lstDevices = New ListBox With {.Dock = DockStyle.Fill, .DataSource = _devices, .BackColor = NightOutTheme.BgPanel, .ForeColor = NightOutTheme.Cream, .BorderStyle = BorderStyle.FixedSingle}
            AddHandler lstDevices.SelectedIndexChanged, Async Sub()
                                                           _selectedDevice = TryCast(lstDevices.SelectedItem, BeaconScanItem)
                                                           Await TryLoadKnownPinForSelectedDeviceAsync()
                                                       End Sub
            left.Controls.Add(lstDevices, 0, 1)

            lblState = New Label With {.Dock = DockStyle.Fill, .Text = "Aucun beacon connecté", .ForeColor = NightOutTheme.Muted, .TextAlign = Drawing.ContentAlignment.MiddleLeft}
            left.Controls.Add(lblState, 0, 2)

            txtLog = New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .ScrollBars = ScrollBars.Vertical, .ReadOnly = True, .BackColor = NightOutTheme.BgPanel, .ForeColor = NightOutTheme.Cream, .BorderStyle = BorderStyle.FixedSingle}
            left.Controls.Add(txtLog, 0, 3)

            Dim right As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 13, .BackColor = NightOutTheme.BgDark, .Padding = New Padding(12, 0, 0, 0)}
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160))
            right.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.Controls.Add(right, 1, 0)

            AddTitle(right, "Configuration Spotiz iBeacon")
            cboBars = New ComboBox With {.Dock = DockStyle.Fill, .DropDownStyle = ComboBoxStyle.DropDownList, .DataSource = _bars}
            AddRow(right, "Bar associé", cboBars)
            txtName = MakeText("SPOTIZ-BEACON")
            AddRow(right, "Nom beacon", txtName)
            txtPin = MakeText("000000")
            AddRow(right, "PIN actuel", txtPin)
            txtNewPin = MakeText("000000")
            AddRow(right, "Nouveau PIN", txtNewPin)
            txtUuid = MakeText("FDA50693-A4E2-4FB1-AFCF-C6EB07647825")
            AddRow(right, "UUID", txtUuid)
            numMajor = MakeNumber(0, 65535, 100)
            AddRow(right, "Major", numMajor)
            numMinor = MakeNumber(0, 65535, 1)
            AddRow(right, "Minor", numMinor)
            numInterval = MakeNumber(100, 10000, 1000)
            AddRow(right, "Intervalle ms", numInterval)
            numDeviceTx = MakeNumber(-40, 10, 0)
            AddRow(right, "Puissance module", numDeviceTx)
            numBeaconTx = MakeNumber(-40, 10, 2)
            AddRow(right, "Puissance broadcast", numBeaconTx)

            Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .BackColor = NightOutTheme.BgDark}
            btnRead = MakeButton("Lire config")
            btnProgram = MakeButton("Programmer")
            btnGeneratePin = MakeButton("Générer PIN")
            btnSaveSupabase = MakeButton("Associer Supabase")
            actions.Controls.AddRange({btnRead, btnProgram, btnGeneratePin, btnSaveSupabase})
            right.Controls.Add(actions, 0, 12)
            right.SetColumnSpan(actions, 2)
        End Sub

        Private Sub AddTitle(panel As TableLayoutPanel, text As String)
            Dim lbl As New Label With {.Text = text, .Dock = DockStyle.Fill, .ForeColor = NightOutTheme.Gold, .Font = NightOutTheme.FontTitle(14.0F), .TextAlign = Drawing.ContentAlignment.MiddleLeft}
            panel.Controls.Add(lbl, 0, 0)
            panel.SetColumnSpan(lbl, 2)
            panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
        End Sub

        Private Sub AddRow(panel As TableLayoutPanel, label As String, control As Control)
            Dim row = panel.RowStyles.Count
            panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
            Dim lbl As New Label With {.Text = label, .Dock = DockStyle.Fill, .ForeColor = NightOutTheme.Muted, .TextAlign = Drawing.ContentAlignment.MiddleLeft}
            panel.Controls.Add(lbl, 0, row)
            panel.Controls.Add(control, 1, row)
        End Sub

        Private Function MakeButton(text As String) As Button
            Return New Button With {.Text = text, .AutoSize = True, .Height = 32, .BackColor = NightOutTheme.BgPanel3, .ForeColor = NightOutTheme.Cream, .FlatStyle = FlatStyle.Flat, .Margin = New Padding(4)}
        End Function

        Private Function MakeText(text As String) As TextBox
            Return New TextBox With {.Text = text, .Dock = DockStyle.Fill, .BackColor = NightOutTheme.BgPanel, .ForeColor = NightOutTheme.Cream, .BorderStyle = BorderStyle.FixedSingle}
        End Function

        Private Function MakeNumber(min As Decimal, max As Decimal, value As Decimal) As NumericUpDown
            Return New NumericUpDown With {.Minimum = min, .Maximum = max, .Value = value, .Dock = DockStyle.Left, .Width = 160, .BackColor = NightOutTheme.BgPanel, .ForeColor = NightOutTheme.Cream}
        End Function

        Private Async Sub BeaconProgrammingForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Dim raw = Await SupabaseClient.GetRawAsync("bars?select=id,name&order=name.asc")
                Dim arr = JArray.Parse(raw)
                Dim bars = arr.Select(Function(x) New BarLookupItem With {.Id = x.Value(Of String)("id"), .Name = x.Value(Of String)("name")}).ToList()
                _bars.DataSource = bars
                If bars.Count > 0 Then cboBars.SelectedIndex = 0
                AppendLogSafe($"{bars.Count} bar(s) chargés depuis Supabase.")
            Catch ex As Exception
                AppendLogSafe("Impossible de charger les bars : " & ex.Message)
            End Try
        End Function

        Private Sub btnScan_Click(sender As Object, e As EventArgs) Handles btnScan.Click
            _devices.Clear()
            _ble.StartScan()
            lblState.Text = "Scan BLE en cours..."
        End Sub

        Private Sub btnStop_Click(sender As Object, e As EventArgs) Handles btnStop.Click
            _ble.StopScan()
            lblState.Text = "Scan arrêté"
        End Sub

        Private Async Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
            If _selectedDevice Is Nothing Then
                MessageBox.Show("Sélectionne d'abord un beacon dans la liste.", "Spotiz Beacon", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            Await RunBusyAsync(Async Function(ct)
                               Await _ble.ConnectAsync(_selectedDevice, txtPin.Text.Trim(), ct)
                               lblState.Text = "Connecté : " & _selectedDevice.AddressText
                           End Function)
        End Sub

        Private Async Sub btnRead_Click(sender As Object, e As EventArgs) Handles btnRead.Click
            Await RunBusyAsync(Async Function(ct)
                               Dim responses = Await _ble.ReadConfigurationAsync(ct)
                               Dim cfg = _ble.ParseConfiguration(responses)
                               ApplyReadConfiguration(cfg)
                               Await SelectAssignedBarForConfigurationAsync(cfg)
                               AppendLogSafe("Lecture terminée : " & responses.Count & " réponse(s). Formulaire mis à jour avec les valeurs lues.")
                           End Function)
        End Sub

        Private Async Sub btnProgram_Click(sender As Object, e As EventArgs) Handles btnProgram.Click
            Dim profile As BeaconProgrammingProfile = Nothing
            If Not TryBuildProfile(profile) Then Return

            Await RunBusyAsync(Async Function(ct)
                               Dim res = Await _ble.ProgramSpotizIBeaconAsync(profile, ct)
                               AppendLogSafe(res.Message)
                               If res.Success Then
                                   txtPin.Text = profile.NewPin
                                   Await SaveBeaconAssignmentAsync(profile, False)
                               End If
                           End Function)
        End Sub

        Private Sub btnGeneratePin_Click(sender As Object, e As EventArgs) Handles btnGeneratePin.Click
            txtNewPin.Text = GeneratePin()
            AppendLogSafe("Nouveau PIN généré. Il sera sauvegardé dans Supabase après programmation.")
        End Sub

        Private Async Sub btnSaveSupabase_Click(sender As Object, e As EventArgs) Handles btnSaveSupabase.Click
            Dim profile As BeaconProgrammingProfile = Nothing
            If Not TryBuildProfile(profile) Then Return

            Await SaveBeaconAssignmentAsync(profile, True)
        End Sub


        Private Sub ApplyReadConfiguration(cfg As BeaconReadConfiguration)
            If cfg Is Nothing Then Return

            If Not String.IsNullOrWhiteSpace(cfg.DeviceName) Then txtName.Text = cfg.DeviceName
            If Not String.IsNullOrWhiteSpace(cfg.Pin) Then
                txtPin.Text = cfg.Pin
                txtNewPin.Text = cfg.Pin
            End If
            If Not String.IsNullOrWhiteSpace(cfg.Uuid) Then txtUuid.Text = cfg.Uuid
            If cfg.Major.HasValue Then numMajor.Value = ClampDecimal(cfg.Major.Value, numMajor.Minimum, numMajor.Maximum)
            If cfg.Minor.HasValue Then numMinor.Value = ClampDecimal(cfg.Minor.Value, numMinor.Minimum, numMinor.Maximum)
            If cfg.BroadcastIntervalMs.HasValue Then numInterval.Value = ClampDecimal(cfg.BroadcastIntervalMs.Value, numInterval.Minimum, numInterval.Maximum)
            If cfg.DeviceTxPower.HasValue Then numDeviceTx.Value = ClampDecimal(cfg.DeviceTxPower.Value, numDeviceTx.Minimum, numDeviceTx.Maximum)

            If String.IsNullOrWhiteSpace(cfg.RawIBeaconPayload) Then
                AppendLogSafe("⚠ BADVDATA slot 0 lu, mais impossible d'extraire UUID/Major/Minor. Regarde le journal brut.")
            Else
                AppendLogSafe("Config lue : UUID=" & txtUuid.Text & " / Major=" & CInt(numMajor.Value).ToString() & " / Minor=" & CInt(numMinor.Value).ToString())
            End If
        End Sub

        Private Shared Function ClampDecimal(value As Integer, min As Decimal, max As Decimal) As Decimal
            Dim d = CDec(value)
            If d < min Then Return min
            If d > max Then Return max
            Return d
        End Function

        Private Function BuildProfile() As BeaconProgrammingProfile
            Dim bar = TryCast(cboBars.SelectedItem, BarLookupItem)
            Return New BeaconProgrammingProfile With {
                .BarId = If(bar?.Id, ""),
                .BarName = If(bar?.Name, ""),
                .DeviceName = txtName.Text.Trim(),
                .Pin = NormalizePin(txtPin.Text.Trim(), "PIN actuel"),
                .NewPin = NormalizePin(txtNewPin.Text.Trim(), "Nouveau PIN"),
                .Uuid = txtUuid.Text.Trim(),
                .Major = CInt(numMajor.Value),
                .Minor = CInt(numMinor.Value),
                .BroadcastIntervalMs = CInt(numInterval.Value),
                .DeviceTxPower = CInt(numDeviceTx.Value),
                .BeaconTxPower = CInt(numBeaconTx.Value),
                .BluetoothAddress = If(_selectedDevice?.AddressText, "")
            }
        End Function

        Private Function TryBuildProfile(ByRef profile As BeaconProgrammingProfile) As Boolean
            Try
                EnsureNewPinForProgramming()
                profile = BuildProfile()
                Return True
            Catch ex As Exception
                AppendLogSafe("Profil beacon invalide : " & ex.Message)
                MessageBox.Show(ex.Message, "Spotiz Beacon", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End Try
        End Function

        Private Async Function SaveBeaconAssignmentAsync(profile As BeaconProgrammingProfile, showMessage As Boolean) As Task
            If String.IsNullOrWhiteSpace(profile.BarId) Then
                AppendLogSafe("Association Supabase ignorée : aucun bar sélectionné.")
                Return
            End If

            Try
                Dim body As New JObject From {
                    {"bar_id", profile.BarId},
                    {"uuid", profile.Uuid},
                    {"beacon_uuid", profile.Uuid},
                    {"major", profile.Major},
                    {"minor", profile.Minor},
                    {"label", "Spotiz beacon - " & profile.BarName},
                    {"min_rssi", -78},
                    {"is_active", True},
                    {"device_name", profile.DeviceName},
                    {"bluetooth_address", profile.BluetoothAddress},
                    {"pin_code", profile.NewPin},
                    {"broadcast_interval_ms", profile.BroadcastIntervalMs},
                    {"device_tx_power", profile.DeviceTxPower},
                    {"beacon_tx_power", profile.BeaconTxPower},
                    {"status", "programmed"},
                    {"programmed_at", DateTime.UtcNow.ToString("O")}
                }
                Await SupabaseClient.UpsertAsync("bar_beacons", New JArray(body), "bar_id,major,minor")
                AppendLogSafe("Beacon associé dans Supabase → " & profile.BarName)
                AppendLogSafe("PIN sauvegardé pour reprogrammation future : " & MaskPin(profile.NewPin))
                If showMessage Then MessageBox.Show("Association Supabase enregistrée.", "Spotiz Beacon", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                AppendLogSafe("Supabase : association non enregistrée. Exécute le SQL de migration bar_beacons puis réessaie. Détail : " & ex.Message)
                If showMessage Then MessageBox.Show(ex.Message, "Supabase", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Function

        Private Async Function RunBusyAsync(action As Func(Of CancellationToken, Task)) As Task
            Try
                SetButtons(False)
                _cts = New CancellationTokenSource(TimeSpan.FromSeconds(45))
                Await action(_cts.Token)
            Catch ex As Exception
                AppendLogSafe("ERREUR : " & ex.Message)
                MessageBox.Show(ex.Message, "Spotiz Beacon", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                SetButtons(True)
                _cts?.Dispose()
                _cts = Nothing
            End Try
        End Function

        Private Sub SetButtons(enabled As Boolean)
            For Each b In {btnScan, btnStop, btnConnect, btnRead, btnProgram, btnGeneratePin, btnSaveSupabase}
                If b IsNot Nothing Then b.Enabled = enabled
            Next
        End Sub

        Private Async Function TryLoadKnownPinForSelectedDeviceAsync() As Task
            Try
                If _selectedDevice Is Nothing OrElse String.IsNullOrWhiteSpace(_selectedDevice.AddressText) Then Return

                Dim address = Uri.EscapeDataString(_selectedDevice.AddressText)
                Dim raw = Await SupabaseClient.GetRawAsync("bar_beacons?select=bar_id,pin_code,device_name,uuid,beacon_uuid,major,minor&bluetooth_address=eq." & address & "&limit=1")
                Dim arr = JArray.Parse(raw)
                If arr.Count = 0 Then Return

                Dim row = DirectCast(arr(0), JObject)
                ApplyKnownBeaconRow(row, "Adresse Bluetooth reconnue")
            Catch ex As Exception
                AppendLogSafe("Info : aucun PIN Supabase retrouvé pour ce beacon BLE (" & ex.Message & ").")
            End Try
        End Function

        Private Async Function SelectAssignedBarForConfigurationAsync(cfg As BeaconReadConfiguration) As Task
            If cfg Is Nothing OrElse String.IsNullOrWhiteSpace(cfg.Uuid) OrElse Not cfg.Major.HasValue OrElse Not cfg.Minor.HasValue Then Return

            Dim row = Await FindBeaconAssignmentAsync(cfg.Uuid, cfg.Major.Value, cfg.Minor.Value)
            If row Is Nothing Then
                row = Await FindBeaconAssignmentByBluetoothAddressAsync()
            End If

            If row Is Nothing Then
                AppendLogSafe("Aucune association Supabase trouvée pour UUID/Major/Minor lus.")
                Return
            End If

            ApplyKnownBeaconRow(row, "Association Supabase trouvée")
        End Function

        Private Async Function FindBeaconAssignmentByBluetoothAddressAsync() As Task(Of JObject)
            Try
                If _selectedDevice Is Nothing OrElse String.IsNullOrWhiteSpace(_selectedDevice.AddressText) Then Return Nothing

                Dim address = Uri.EscapeDataString(_selectedDevice.AddressText)
                Dim raw = Await SupabaseClient.GetRawAsync("bar_beacons?select=bar_id,pin_code,device_name,uuid,beacon_uuid,major,minor,bluetooth_address&bluetooth_address=eq." & address & "&limit=1")
                Dim arr = JArray.Parse(raw)
                If arr.Count > 0 Then
                    AppendLogSafe("Association retrouvée par adresse Bluetooth.")
                    Return DirectCast(arr(0), JObject)
                End If
            Catch ex As Exception
                AppendLogSafe("Recherche association par adresse Bluetooth impossible : " & ex.Message)
            End Try

            Return Nothing
        End Function

        Private Async Function FindBeaconAssignmentAsync(uuid As String, major As Integer, minor As Integer) As Task(Of JObject)
            Dim expectedUuid = NormalizeUuidForCompare(uuid)

            Try
                Dim raw = Await SupabaseClient.GetRawAsync($"bar_beacons?select=bar_id,pin_code,device_name,uuid,beacon_uuid,major,minor,bluetooth_address&major=eq.{major}&minor=eq.{minor}&limit=500")
                Dim arr = JArray.Parse(raw)

                For Each token In arr
                    Dim row = DirectCast(token, JObject)
                    Dim candidateUuid = NormalizeUuidForCompare(If(row.Value(Of String)("beacon_uuid"), row.Value(Of String)("uuid")))
                    If String.Equals(candidateUuid, expectedUuid, StringComparison.OrdinalIgnoreCase) Then
                        Return row
                    End If
                Next

                AppendLogSafe($"Recherche Supabase : {arr.Count} beacon(s) avec Major={major}/Minor={minor}, mais UUID différent.")
            Catch ex As Exception
                AppendLogSafe("Recherche association par UUID/Major/Minor impossible : " & ex.Message)
            End Try

            Return Nothing
        End Function

        Private Shared Function NormalizeUuidForCompare(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return ""

            Dim g As Guid
            If Guid.TryParse(value.Trim(), g) Then
                Return g.ToString("N").ToUpperInvariant()
            End If

            Return New String(value.Trim().
                Where(Function(ch) Uri.IsHexDigit(ch)).
                Select(Function(ch) Char.ToUpperInvariant(ch)).
                ToArray())
        End Function

        Private Sub ApplyKnownBeaconRow(row As JObject, sourceLabel As String)
            Dim barId = row.Value(Of String)("bar_id")
            If Not String.IsNullOrWhiteSpace(barId) AndAlso SelectBarById(barId) Then
                Dim bar = TryCast(cboBars.SelectedItem, BarLookupItem)
                AppendLogSafe(sourceLabel & " : bar associé = " & If(bar?.Name, barId))
            Else
                AppendLogSafe(sourceLabel & " : bar_id Supabase introuvable dans la liste chargée (" & If(barId, "null") & ").")
            End If

            Dim storedPin = row.Value(Of String)("pin_code")
            If Not String.IsNullOrWhiteSpace(storedPin) Then
                txtPin.Text = storedPin.Trim()
                txtNewPin.Text = storedPin.Trim()
                AppendLogSafe("PIN connu récupéré depuis Supabase : " & MaskPin(storedPin))
            End If
        End Sub

        Private Function SelectBarById(barId As String) As Boolean
            If String.IsNullOrWhiteSpace(barId) Then Return False

            Dim list = DirectCast(_bars.List, IList)
            For i = 0 To list.Count - 1
                Dim bar = TryCast(list(i), BarLookupItem)
                If bar IsNot Nothing AndAlso String.Equals(bar.Id, barId, StringComparison.OrdinalIgnoreCase) Then
                    cboBars.SelectedIndex = i
                    Return True
                End If
            Next

            Return False
        End Function

        Private Sub EnsureNewPinForProgramming()
            Dim currentPin = NormalizePin(txtPin.Text.Trim(), "PIN actuel")
            Dim newPin = NormalizePin(txtNewPin.Text.Trim(), "Nouveau PIN")

            If String.Equals(currentPin, newPin, StringComparison.Ordinal) Then
                txtNewPin.Text = GeneratePin()
                AppendLogSafe("Nouveau PIN identique au PIN actuel : PIN sécurisé généré automatiquement.")
            End If
        End Sub

        Private Shared Function NormalizePin(value As String, label As String) As String
            If String.IsNullOrWhiteSpace(value) Then Throw New ArgumentException(label & " obligatoire.")
            Dim pin = value.Trim()
            If pin.Length <> 6 OrElse pin.Any(Function(ch) Not Char.IsDigit(ch)) Then
                Throw New ArgumentException(label & " doit contenir exactement 6 chiffres.")
            End If
            Return pin
        End Function

        Private Shared Function GeneratePin() As String
            Return RandomNumberGenerator.GetInt32(0, 1000000).ToString("000000")
        End Function

        Private Shared Function MaskPin(pin As String) As String
            If String.IsNullOrWhiteSpace(pin) OrElse pin.Length < 2 Then Return "******"
            Return "****" & pin.Substring(pin.Length - 2)
        End Function

        Private Sub OnDeviceFound(device As BeaconScanItem)
            If Me.IsDisposed Then Return
            If Me.InvokeRequired Then
                Me.BeginInvoke(New Action(Of BeaconScanItem)(AddressOf OnDeviceFound), device)
                Return
            End If

            Dim list = DirectCast(_devices.List, IList)
            Dim existingIndex = -1
            For i = 0 To list.Count - 1
                Dim cur = TryCast(list(i), BeaconScanItem)
                If cur IsNot Nothing AndAlso cur.BluetoothAddress = device.BluetoothAddress Then
                    existingIndex = i
                    Exit For
                End If
            Next
            If existingIndex < 0 Then
                _devices.Add(device)
            Else
                _devices.ResetItem(existingIndex)
            End If
        End Sub

        Private Sub AppendLogSafe(message As String)
            If Me.IsDisposed Then Return
            If Me.InvokeRequired Then
                Me.BeginInvoke(New Action(Of String)(AddressOf AppendLogSafe), message)
                Return
            End If
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}")
        End Sub

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            _ble.Dispose()
            MyBase.OnFormClosed(e)
        End Sub

    End Class

End Namespace
