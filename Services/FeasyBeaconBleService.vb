Option Strict On
Option Explicit On

Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports Windows.Devices.Bluetooth
Imports Windows.Devices.Bluetooth.Advertisement
Imports Windows.Devices.Bluetooth.GenericAttributeProfile
Imports Windows.Storage.Streams
Imports NightOutAdmin.Models

Namespace Services

    ''' <summary>
    ''' Couche BLE Windows pour les beacons Feasycom.
    ''' Elle ne dépend pas du SDK Android : elle parle directement avec le service GATT Feasycom FFF0.
    ''' </summary>
    Public Class FeasyBeaconBleService
        Implements IDisposable

        Public Event DeviceFound(device As BeaconScanItem)
        Public Event Log(message As String)
        Public Event ResponseReceived(response As String)

        Private Shared ReadOnly FeasyServiceUuid As Guid = Guid.Parse("0000fff0-0000-1000-8000-00805f9b34fb")
        Private Shared ReadOnly ClientConfigUuid As Guid = Guid.Parse("00002902-0000-1000-8000-00805f9b34fb")

        Private _watcher As BluetoothLEAdvertisementWatcher
        Private ReadOnly _devices As New Dictionary(Of ULong, BeaconScanItem)()
        Private _device As BluetoothLEDevice
        Private _service As GattDeviceService
        Private _writeCharacteristic As GattCharacteristic
        Private _notifyCharacteristic As GattCharacteristic
        Private ReadOnly _responses As New List(Of String)()
        Private ReadOnly _sync As New Object()
        Private _currentDeviceAddressText As String = String.Empty
        Private _atEngineOpened As Boolean = False

        Public ReadOnly Property IsConnected As Boolean
            Get
                Return _device IsNot Nothing AndAlso _writeCharacteristic IsNot Nothing
            End Get
        End Property

        Public Sub StartScan()
            StopScan()
            _devices.Clear()

            _watcher = New BluetoothLEAdvertisementWatcher() With {
                .ScanningMode = BluetoothLEScanningMode.Active
            }
            AddHandler _watcher.Received, AddressOf OnAdvertisementReceived
            AddHandler _watcher.Stopped, Sub() RaiseEvent Log("Scan BLE arrêté.")
            _watcher.Start()
            RaiseEvent Log("Scan BLE démarré. Mets le beacon Feasycom à proximité du PC.")
        End Sub

        Public Sub StopScan()
            If _watcher IsNot Nothing Then
                Try
                    RemoveHandler _watcher.Received, AddressOf OnAdvertisementReceived
                    _watcher.Stop()
                Catch
                End Try
                _watcher = Nothing
            End If
        End Sub

        Private Sub OnAdvertisementReceived(sender As BluetoothLEAdvertisementWatcher, args As BluetoothLEAdvertisementReceivedEventArgs)
            Dim name = If(args.Advertisement.LocalName, String.Empty)
            Dim manufacturerHex = BuildManufacturerHex(args)
            Dim addressText = FormatBluetoothAddress(args.BluetoothAddress)
            Dim isFeasy = name.IndexOf("Feasy", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                          name.IndexOf("FSC", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                          manufacturerHex.IndexOf("FEASY", StringComparison.OrdinalIgnoreCase) >= 0

            Dim item As BeaconScanItem = Nothing
            SyncLock _sync
                If Not _devices.TryGetValue(args.BluetoothAddress, item) Then
                    item = New BeaconScanItem With {
                        .BluetoothAddress = args.BluetoothAddress,
                        .AddressText = addressText
                    }
                    _devices(args.BluetoothAddress) = item
                End If
                item.Name = If(String.IsNullOrWhiteSpace(name), item.Name, name)
                item.LocalName = name
                item.Rssi = args.RawSignalStrengthInDBm
                item.LastSeenUtc = DateTime.UtcNow
                item.IsFeasycom = item.IsFeasycom OrElse isFeasy
                item.ManufacturerHex = manufacturerHex
            End SyncLock

            RaiseEvent DeviceFound(item)
        End Sub

        Public Async Function ConnectAsync(item As BeaconScanItem, pin As String, Optional ct As CancellationToken = Nothing) As Task
            If item Is Nothing Then Throw New ArgumentNullException(NameOf(item))
            Disconnect()
            StopScan()
            RaiseEvent Log($"Connexion à {item.AddressText}...")
            _currentDeviceAddressText = item.AddressText

            _device = Await BluetoothLEDevice.FromBluetoothAddressAsync(item.BluetoothAddress).AsTask(ct)
            If _device Is Nothing Then Throw New InvalidOperationException("Windows n'a pas réussi à ouvrir le périphérique BLE.")

            Dim result = Await _device.GetGattServicesForUuidAsync(FeasyServiceUuid, BluetoothCacheMode.Uncached).AsTask(ct)
            If result.Status <> GattCommunicationStatus.Success OrElse result.Services.Count = 0 Then
                Throw New InvalidOperationException("Service Feasycom FFF0 introuvable. Le beacon doit être connectable et déverrouillé.")
            End If

            _service = result.Services(0)
            Dim charsResult = Await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct)
            If charsResult.Status <> GattCommunicationStatus.Success Then
                Throw New InvalidOperationException("Impossible de lire les caractéristiques GATT du beacon.")
            End If

            For Each ch In charsResult.Characteristics
                If (ch.CharacteristicProperties And GattCharacteristicProperties.Notify) = GattCharacteristicProperties.Notify Then
                    _notifyCharacteristic = ch
                End If
                If (ch.CharacteristicProperties And GattCharacteristicProperties.Write) = GattCharacteristicProperties.Write OrElse
                   (ch.CharacteristicProperties And GattCharacteristicProperties.WriteWithoutResponse) = GattCharacteristicProperties.WriteWithoutResponse Then
                    _writeCharacteristic = ch
                End If
            Next

            If _writeCharacteristic Is Nothing Then Throw New InvalidOperationException("Caractéristique d'écriture introuvable.")
            If _notifyCharacteristic IsNot Nothing Then
                AddHandler _notifyCharacteristic.ValueChanged, AddressOf OnNotifyValueChanged
                Await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(ct)
            End If

            RaiseEvent Log("Connecté au service Feasycom FFF0.")

            If Not String.IsNullOrWhiteSpace(pin) Then
                Await AuthenticateAsync(pin, ct)
                Await OpenAtEngineAsync(ct)
            End If
        End Function

        Private Async Function AuthenticateAsync(pin As String, Optional ct As CancellationToken = Nothing) As Task
            Dim cleanPin = New String(pin.Where(Function(c) Char.IsDigit(c)).ToArray())
            If String.IsNullOrWhiteSpace(cleanPin) Then cleanPin = "000000"

            RaiseEvent Log("Authentification Feasycom AUTH LAB v4...")
            RaiseEvent Log("Code utilisé : " & MaskPin(cleanPin) & " — test SDK natif exact : AUTH + 16 bytes chiffrés, clé Feasycom, rounds=16.")

            Dim candidates = BuildSdkAuthCandidates(_currentDeviceAddressText, cleanPin)
            Dim index = 1

            For Each candidate In candidates
                RaiseEvent Log($"AUTH test {index}/{candidates.Count} : {candidate.Label}")
                Await SendRawAsync(candidate.Payload, "AUTH(" & candidate.Label & ")" & MaskPin(cleanPin), ct)
                Await Task.Delay(450, ct)

                If IsAuthenticationAccepted() Then
                    RaiseEvent Log("Authentification Feasycom acceptée avec : " & candidate.Label)
                    Return
                End If

                index += 1
            Next

            RaiseEvent Log("AUTH non validée. Si cette v4 échoue, il faut capturer la trame AUTH exacte envoyée par l'APK Feasycom pour comparer byte par byte.")
        End Function

        Private Async Function OpenAtEngineAsync(Optional ct As CancellationToken = Nothing) As Task
            If _atEngineOpened Then Return

            RaiseEvent Log("Ouverture du moteur AT Feasycom...")
            Dim before As Integer
            SyncLock _sync
                before = _responses.Count
            End SyncLock

            ' Le SDK Android envoie exactement cette trame sans CR/LF avant les commandes AT.
            Await SendRawAsync(Encoding.ASCII.GetBytes("$OpenFscAtEngine$"), "$OpenFscAtEngine$", ct)
            Dim responses = Await WaitForResponsesAsync(before, 2500, ct)

            If responses.Any(Function(r) r.IndexOf("Opened", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                      r.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                      r.IndexOf("AUTHOK", StringComparison.OrdinalIgnoreCase) >= 0) Then
                _atEngineOpened = True
                RaiseEvent Log("Moteur AT Feasycom ouvert.")
            Else
                RaiseEvent Log("Moteur AT : aucune confirmation lisible. On continue quand même, mais si les commandes restent muettes, c'est cette étape qu'il faudra recapturer.")
            End If
        End Function

        Private Async Function WaitForResponsesAsync(beforeCount As Integer, timeoutMs As Integer, Optional ct As CancellationToken = Nothing) As Task(Of List(Of String))
            Dim start = DateTime.UtcNow
            Do
                ct.ThrowIfCancellationRequested()
                SyncLock _sync
                    If _responses.Count > beforeCount Then
                        Return _responses.Skip(beforeCount).ToList()
                    End If
                End SyncLock
                If (DateTime.UtcNow - start).TotalMilliseconds >= timeoutMs Then Exit Do
                Await Task.Delay(80, ct)
            Loop
            SyncLock _sync
                Return _responses.Skip(beforeCount).ToList()
            End SyncLock
        End Function

        Private Class AuthCandidate
            Public Property Label As String = String.Empty
            Public Property Payload As Byte() = Array.Empty(Of Byte)()
        End Class

        Private Shared Function BuildSdkAuthCandidates(addressText As String, pin As String) As List(Of AuthCandidate)
            Dim result As New List(Of AuthCandidate)()
            Dim macHex = New String(If(addressText, String.Empty).Where(Function(c) Uri.IsHexDigit(c)).ToArray()).ToUpperInvariant()
            If macHex.Length < 12 Then Throw New InvalidOperationException("Adresse MAC BLE invalide pour générer l'auth Feasycom.")
            macHex = macHex.Substring(macHex.Length - 12, 12)

            Dim macNormal = HexToBytes(macHex)
            Dim macReversed = CType(macNormal.Clone(), Byte())
            Array.Reverse(macReversed)

            Dim pinDigits = New String(If(pin, "000000").Where(Function(c) Char.IsDigit(c)).ToArray())
            If String.IsNullOrWhiteSpace(pinDigits) Then pinDigits = "000000"
            Dim pinNumber As UInteger = 0UI
            UInteger.TryParse(pinDigits, pinNumber)

            Dim pinLittle = BitConverter.GetBytes(pinNumber)
            Dim pinBig = CType(pinLittle.Clone(), Byte())
            If BitConverter.IsLittleEndian Then Array.Reverse(pinBig)

            Dim keyBytes As Byte() = {&HC5, &HBA, &HE8, &H68, &H22, &H3F, &H9F, &H50, &H96, &H87, &H17, &HB2, &H40, &H21, &HC5, &H11}
            Dim keyLittle As UInteger() = {
                BitConverter.ToUInt32(keyBytes, 0),
                BitConverter.ToUInt32(keyBytes, 4),
                BitConverter.ToUInt32(keyBytes, 8),
                BitConverter.ToUInt32(keyBytes, 12)
            }
            Dim keyBig As UInteger() = {
                ToUInt32BigEndian(keyBytes, 0),
                ToUInt32BigEndian(keyBytes, 4),
                ToUInt32BigEndian(keyBytes, 8),
                ToUInt32BigEndian(keyBytes, 12)
            }

            Dim rngBytes(5) As Byte
            Using rng = System.Security.Cryptography.RandomNumberGenerator.Create()
                rng.GetBytes(rngBytes)
            End Using

            ' Variante exacte reconstruite depuis libencrypted.so :
            ' plaintext = MAC(6) + PIN uint32 little endian(4) + random(6)
            ' chiffrement de 2 blocs de 8 octets avec l'algorithme Feasycom TEA-like, 16 rounds.
            AddEncryptedCandidateNativeExact(result, "sdk-native-exact-16rounds", macNormal, pinLittle, rngBytes, keyLittle)

            Dim macVariants = New List(Of Tuple(Of String, Byte())) From {
                Tuple.Create("mac-normal", macNormal),
                Tuple.Create("mac-reversed", macReversed)
            }
            Dim pinVariants = New List(Of Tuple(Of String, Byte())) From {
                Tuple.Create("pin-little", pinLittle),
                Tuple.Create("pin-big", pinBig)
            }
            Dim keyVariants = New List(Of Tuple(Of String, UInteger())) From {
                Tuple.Create("key-little", keyLittle),
                Tuple.Create("key-big", keyBig)
            }

            For Each macVariant In macVariants
                For Each pinVariant In pinVariants
                    For Each keyVariant In keyVariants
                        AddEncryptedCandidate(result, macVariant.Item1 & "+" & pinVariant.Item1 & "+" & keyVariant.Item1 & "+xtea", macVariant.Item2, pinVariant.Item2, rngBytes, keyVariant.Item2, True)
                        AddEncryptedCandidate(result, macVariant.Item1 & "+" & pinVariant.Item1 & "+" & keyVariant.Item1 & "+tea", macVariant.Item2, pinVariant.Item2, rngBytes, keyVariant.Item2, False)
                    Next
                Next
            Next

            result.Add(New AuthCandidate With {.Label = "plain-hex-pin", .Payload = BuildAuthHexPayload(pinDigits)})
            result.Add(New AuthCandidate With {.Label = "plain-ascii-pin", .Payload = Encoding.ASCII.GetBytes("AUTH" & pinDigits)})
            result.Add(New AuthCandidate With {.Label = "pin-only-ascii", .Payload = Encoding.ASCII.GetBytes(pinDigits)})
            Return result
        End Function

        Private Shared Sub AddEncryptedCandidateNativeExact(list As List(Of AuthCandidate), label As String, mac As Byte(), pinBytes As Byte(), rnd As Byte(), key As UInteger())
            Dim block(15) As Byte
            System.Buffer.BlockCopy(mac, 0, block, 0, 6)
            System.Buffer.BlockCopy(pinBytes, 0, block, 6, 4)
            System.Buffer.BlockCopy(rnd, 0, block, 10, 6)

            EncryptFeasyNativeBlock(block, 0, key, 16)
            EncryptFeasyNativeBlock(block, 8, key, 16)

            Dim auth = Encoding.ASCII.GetBytes("AUTH")
            Dim payload(auth.Length + block.Length - 1) As Byte
            System.Buffer.BlockCopy(auth, 0, payload, 0, auth.Length)
            System.Buffer.BlockCopy(block, 0, payload, auth.Length, block.Length)
            list.Add(New AuthCandidate With {.Label = label, .Payload = payload})
        End Sub

        Private Shared Sub AddEncryptedCandidate(list As List(Of AuthCandidate), label As String, mac As Byte(), pinBytes As Byte(), rnd As Byte(), key As UInteger(), useXtea As Boolean)
            Dim block(15) As Byte
            System.Buffer.BlockCopy(mac, 0, block, 0, 6)
            System.Buffer.BlockCopy(pinBytes, 0, block, 6, 4)
            System.Buffer.BlockCopy(rnd, 0, block, 10, 6)

            If useXtea Then
                EncryptFeasyBlockXtea(block, 0, key)
                EncryptFeasyBlockXtea(block, 8, key)
            Else
                EncryptFeasyBlockTea(block, 0, key)
                EncryptFeasyBlockTea(block, 8, key)
            End If

            Dim auth = Encoding.ASCII.GetBytes("AUTH")
            Dim payload(auth.Length + block.Length - 1) As Byte
            System.Buffer.BlockCopy(auth, 0, payload, 0, auth.Length)
            System.Buffer.BlockCopy(block, 0, payload, auth.Length, block.Length)
            list.Add(New AuthCandidate With {.Label = label, .Payload = payload})
        End Sub

        Private Shared Function ToUInt32BigEndian(data As Byte(), offset As Integer) As UInteger
            Return (CUInt(data(offset)) << 24) Or (CUInt(data(offset + 1)) << 16) Or (CUInt(data(offset + 2)) << 8) Or CUInt(data(offset + 3))
        End Function

        Private Shared Function BuildSdkEncryptedAuthPayload(addressText As String, pin As String) As Byte()
            Return BuildSdkAuthCandidates(addressText, pin).First().Payload
        End Function

        Private Shared Function BuildFeasyEncryptedPasswordHex(addressText As String, pin As String) As String
            Dim payload = BuildSdkEncryptedAuthPayload(addressText, pin)
            Return BitConverter.ToString(payload.Skip(4).ToArray()).Replace("-", String.Empty)
        End Function

        Private Shared Sub EncryptFeasyNativeBlock(buffer As Byte(), offset As Integer, key As UInteger(), rounds As Integer)
            Dim v0 As UInteger = BitConverter.ToUInt32(buffer, offset)
            Dim v1 As UInteger = BitConverter.ToUInt32(buffer, offset + 4)
            Dim sum As UInteger = 0UI
            Dim delta As UInteger = CUInt(&H9E3779B9UL)

            For i = 1 To rounds
                v0 = Add32(v0,
                           Add32((v1 << 4) Xor (v1 >> 5),
                                 v1 Xor sum,
                                 key(CInt(sum And 3UI))))

                sum = Add32(sum, delta)

                v1 = Add32(v1,
                           Add32((v0 << 4) Xor (v0 >> 5),
                                 v0 Xor sum,
                                 key(CInt((sum >> 11) And 3UI))))
            Next

            WriteUInt32Little(buffer, offset, v0)
            WriteUInt32Little(buffer, offset + 4, v1)
        End Sub

        Private Shared Sub EncryptFeasyBlockXtea(buffer As Byte(), offset As Integer, key As UInteger())
            Dim v0 = BitConverter.ToUInt32(buffer, offset)
            Dim v1 = BitConverter.ToUInt32(buffer, offset + 4)
            Dim sum As UInteger = 0UI
            Dim delta As UInteger = CUInt(&H9E3779B9UL)

            For i = 1 To 32
                Dim previousSum = sum
                v0 = Add32(v0, Add32((v1 << 4) Xor (v1 >> 5), v1) Xor Add32(previousSum, key(CInt(previousSum And 3UI))))
                sum = Add32(sum, delta)
                v1 = Add32(v1, Add32((v0 << 4) Xor (v0 >> 5), v0) Xor Add32(sum, key(CInt((sum >> 11) And 3UI))))
            Next

            WriteUInt32Little(buffer, offset, v0)
            WriteUInt32Little(buffer, offset + 4, v1)
        End Sub

        Private Shared Sub EncryptFeasyBlockTea(buffer As Byte(), offset As Integer, key As UInteger())
            Dim v0 = BitConverter.ToUInt32(buffer, offset)
            Dim v1 = BitConverter.ToUInt32(buffer, offset + 4)
            Dim sum As UInteger = 0UI
            Dim delta As UInteger = CUInt(&H9E3779B9UL)

            For i = 1 To 32
                sum = Add32(sum, delta)
                v0 = Add32(v0, Add32((v1 << 4), key(0)) Xor Add32(v1, sum) Xor Add32((v1 >> 5), key(1)))
                v1 = Add32(v1, Add32((v0 << 4), key(2)) Xor Add32(v0, sum) Xor Add32((v0 >> 5), key(3)))
            Next

            WriteUInt32Little(buffer, offset, v0)
            WriteUInt32Little(buffer, offset + 4, v1)
        End Sub

        Private Shared Sub WriteUInt32Little(buffer As Byte(), offset As Integer, value As UInteger)
            Dim b = BitConverter.GetBytes(value)
            System.Buffer.BlockCopy(b, 0, buffer, offset, 4)
        End Sub

        Private Shared Function Add32(ParamArray values As UInteger()) As UInteger
            Dim acc As ULong = 0UL
            For Each value In values
                acc = (acc + value) And &HFFFFFFFFUL
            Next
            Return CUInt(acc)
        End Function

        Private Shared Function HexToBytes(hex As String) As Byte()
            Dim clean = New String(If(hex, String.Empty).Where(Function(c) Uri.IsHexDigit(c)).ToArray())
            If clean.Length Mod 2 = 1 Then clean = "0" & clean
            If clean.Length = 0 Then Return Array.Empty(Of Byte)()
            Dim data((clean.Length \ 2) - 1) As Byte
            For i = 0 To data.Length - 1
                data(i) = Convert.ToByte(clean.Substring(i * 2, 2), 16)
            Next
            Return data
        End Function

        Private Shared Sub AddAuthCandidate(list As List(Of String), value As String)
            If String.IsNullOrWhiteSpace(value) Then Return
            Dim cleaned = New String(value.Where(Function(c) Uri.IsHexDigit(c)).ToArray()).ToUpperInvariant()
            If cleaned.Length = 0 Then Return
            If Not list.Contains(cleaned) Then list.Add(cleaned)
        End Sub

        Private Shared Function BuildAuthHexPayload(pinOrHex As String) As Byte()
            Dim hex = New String(pinOrHex.Where(Function(c) Uri.IsHexDigit(c)).ToArray())
            If hex.Length Mod 2 = 1 Then hex = "0" & hex
            Dim auth = Encoding.ASCII.GetBytes("AUTH")
            Dim data((auth.Length + (hex.Length \ 2)) - 1) As Byte
            System.Buffer.BlockCopy(auth, 0, data, 0, auth.Length)
            For i = 0 To (hex.Length \ 2) - 1
                data(auth.Length + i) = Convert.ToByte(hex.Substring(i * 2, 2), 16)
            Next
            Return data
        End Function

        Private Shared Function MaskPin(value As String) As String
            If String.IsNullOrEmpty(value) Then Return "******"
            Return New String("*"c, value.Length)
        End Function

        Private Function IsAuthenticationAccepted() As Boolean
            SyncLock _sync
                Return _responses.Any(Function(r) r.IndexOf("AUTHOK", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                          r.IndexOf("AUTH OK", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                          r.IndexOf("OK+AUTH", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                          r.Equals("OK", StringComparison.OrdinalIgnoreCase))
            End SyncLock
        End Function

        Public Async Function ReadConfigurationAsync(Optional ct As CancellationToken = Nothing) As Task(Of List(Of String))
            Dim before As Integer
            SyncLock _sync
                before = _responses.Count
            End SyncLock
            If Not _atEngineOpened Then Await OpenAtEngineAsync(ct)

            For Each cmd In FeasyBeaconCommandBuilder.BuildReadConfigurationCommands()
                Dim commandBefore As Integer
                SyncLock _sync
                    commandBefore = _responses.Count
                End SyncLock
                Await SendCommandAsync(cmd, ct)
                Dim received = Await WaitForResponsesAsync(commandBefore, 1800, ct)
                If received.Count = 0 Then RaiseEvent Log("⚠ Aucune réponse pour " & cmd)
                Await Task.Delay(120, ct)
            Next
            SyncLock _sync
                Return _responses.Skip(before).ToList()
            End SyncLock
        End Function

        Public Function ParseConfiguration(responses As IEnumerable(Of String)) As BeaconReadConfiguration
            Dim cfg As New BeaconReadConfiguration()
            If responses Is Nothing Then Return cfg

            Dim raw = String.Join(Environment.NewLine, responses.Where(Function(r) Not String.IsNullOrWhiteSpace(r)))
            cfg.RawText = raw

            Dim m = Regex.Match(raw, "\+NAME=([^\r\n]+)", RegexOptions.IgnoreCase)
            If m.Success Then cfg.DeviceName = m.Groups(1).Value.Trim()

            m = Regex.Match(raw, "\+PIN=([^\r\n]+)", RegexOptions.IgnoreCase)
            If m.Success Then cfg.Pin = m.Groups(1).Value.Trim()

            m = Regex.Match(raw, "\+ADVIN=(\d+)", RegexOptions.IgnoreCase)
            If m.Success Then cfg.BroadcastIntervalMs = Integer.Parse(m.Groups(1).Value)

            m = Regex.Match(raw, "\+TXPOWER=(-?\d+)", RegexOptions.IgnoreCase)
            If m.Success Then cfg.DeviceTxPower = Integer.Parse(m.Groups(1).Value)

            m = Regex.Match(raw, "\+TLM=(\d+)", RegexOptions.IgnoreCase)
            If m.Success Then cfg.TlmEnabled = (m.Groups(1).Value = "1")

            Dim compact = Regex.Replace(raw, "\s+", "")
            Dim payloadMatch = Regex.Match(compact, "\+BADVDATA=,?0,([0-9A-Fa-f]{54,}),[01]", RegexOptions.IgnoreCase)
            If Not payloadMatch.Success Then
                payloadMatch = Regex.Match(compact, "\+BADVDATA=0,([0-9A-Fa-f]{54,}),[01]", RegexOptions.IgnoreCase)
            End If

            If payloadMatch.Success Then
                Dim payload = payloadMatch.Groups(1).Value.ToUpperInvariant()
                cfg.RawIBeaconPayload = payload
                Dim marker = "4C000215"
                Dim idx = payload.IndexOf(marker, StringComparison.OrdinalIgnoreCase)
                If idx >= 0 AndAlso payload.Length >= idx + marker.Length + 32 + 4 + 4 + 2 Then
                    Dim pos = idx + marker.Length
                    Dim uuidNoDash = payload.Substring(pos, 32)
                    cfg.Uuid = FormatUuid(uuidNoDash)
                    cfg.Major = Convert.ToInt32(payload.Substring(pos + 32, 4), 16)
                    cfg.Minor = Convert.ToInt32(payload.Substring(pos + 36, 4), 16)
                End If
            End If

            Return cfg
        End Function

        Private Shared Function FormatUuid(uuidNoDash As String) As String
            Dim clean = New String(If(uuidNoDash, String.Empty).Where(Function(c) Uri.IsHexDigit(c)).ToArray()).ToUpperInvariant()
            If clean.Length <> 32 Then Return uuidNoDash
            Return clean.Substring(0, 8) & "-" & clean.Substring(8, 4) & "-" & clean.Substring(12, 4) & "-" & clean.Substring(16, 4) & "-" & clean.Substring(20, 12)
        End Function

        Public Async Function ProgramSpotizIBeaconAsync(profile As BeaconProgrammingProfile, Optional ct As CancellationToken = Nothing) As Task(Of BeaconProgrammingResult)
            Dim result As New BeaconProgrammingResult()
            Try
                Dim commands = FeasyBeaconCommandBuilder.BuildSpotizIBeaconCommands(profile)
                result.Commands.AddRange(commands)

                If Not _atEngineOpened Then Await OpenAtEngineAsync(ct)

                For Each cmd In commands
                    Dim commandBefore As Integer
                    SyncLock _sync
                        commandBefore = _responses.Count
                    End SyncLock
                    Await SendCommandAsync(cmd, ct)
                    Dim received = Await WaitForResponsesAsync(commandBefore, 2200, ct)
                    If received.Count = 0 Then RaiseEvent Log("⚠ Aucune réponse pour " & cmd)
                    Await Task.Delay(180, ct)
                Next

                SyncLock _sync
                    result.Responses.AddRange(_responses)
                End SyncLock
                result.Success = True
                result.Message = "Programmation envoyée au beacon. Vérifie les réponses OK puis relance un scan pour contrôler l'UUID/Major/Minor."
                Return result
            Catch ex As Exception
                result.Success = False
                result.Message = ex.Message
                Return result
            End Try
        End Function

        Public Async Function SendCommandAsync(command As String, Optional ct As CancellationToken = Nothing) As Task
            If String.IsNullOrWhiteSpace(command) Then Return

            Dim payload = command.Trim()
            If Not payload.EndsWith(vbCrLf, StringComparison.Ordinal) Then payload &= vbCrLf

            Await SendRawAsync(Encoding.ASCII.GetBytes(payload), command, ct)
        End Function

        Private Async Function SendRawAsync(bytes As Byte(), display As String, Optional ct As CancellationToken = Nothing) As Task
            If _writeCharacteristic Is Nothing Then Throw New InvalidOperationException("Aucun beacon connecté.")
            If bytes Is Nothing OrElse bytes.Length = 0 Then Return

            ' Feasycom, comme beaucoup de modules BLE UART, attend des paquets GATT de 20 octets max.
            ' L'auth fait exactement 20 octets, mais BADVDATA dépasse largement 20 octets : sans découpage,
            ' Windows accepte parfois l'écriture mais le beacon ignore la commande.
            Dim offset = 0
            Dim chunkIndex = 1
            Dim totalChunks = CInt(Math.Ceiling(bytes.Length / 20.0R))

            While offset < bytes.Length
                ct.ThrowIfCancellationRequested()
                Dim chunkLength = Math.Min(20, bytes.Length - offset)
                Dim chunk(chunkLength - 1) As Byte
                System.Buffer.BlockCopy(bytes, offset, chunk, 0, chunkLength)

                Await WriteChunkAsync(chunk, display, ct)

                If totalChunks > 1 Then
                    RaiseEvent Log($"   paquet {chunkIndex}/{totalChunks} ({chunkLength} octets)")
                    Await Task.Delay(70, ct)
                End If

                offset += chunkLength
                chunkIndex += 1
            End While

            RaiseEvent Log("→ " & display)
        End Function

        Private Async Function WriteChunkAsync(chunk As Byte(), display As String, Optional ct As CancellationToken = Nothing) As Task
            Dim status As GattCommunicationStatus = GattCommunicationStatus.Unreachable

            ' Le SDK Android écrit avec writeCharacteristic. Sous Windows, certains beacons Feasycom
            ' acceptent mieux WriteWithoutResponse sur les paquets suivants, mais l'auth veut souvent WithResponse.
            If (_writeCharacteristic.CharacteristicProperties And GattCharacteristicProperties.Write) = GattCharacteristicProperties.Write Then
                Using writer As New DataWriter()
                    writer.WriteBytes(chunk)
                    status = Await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse).AsTask(ct)
                End Using
            End If

            If status <> GattCommunicationStatus.Success AndAlso
               (_writeCharacteristic.CharacteristicProperties And GattCharacteristicProperties.WriteWithoutResponse) = GattCharacteristicProperties.WriteWithoutResponse Then
                Using writer As New DataWriter()
                    writer.WriteBytes(chunk)
                    status = Await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse).AsTask(ct)
                End Using
            End If

            If status <> GattCommunicationStatus.Success Then Throw New InvalidOperationException($"Trame non envoyée : {display}")
        End Function

        Private Sub OnNotifyValueChanged(sender As GattCharacteristic, args As GattValueChangedEventArgs)
            If args.CharacteristicValue.Length = 0UI Then Return
            Dim data(CInt(args.CharacteristicValue.Length) - 1) As Byte
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data)
            Dim text = Encoding.ASCII.GetString(data).Trim(ChrW(0), ControlChars.Cr, ControlChars.Lf, " "c)
            If String.IsNullOrWhiteSpace(text) Then Return
            SyncLock _sync
                _responses.Add(text)
            End SyncLock
            RaiseEvent ResponseReceived(text)
            RaiseEvent Log("← " & text)
        End Sub

        Public Sub Disconnect()
            If _notifyCharacteristic IsNot Nothing Then
                Try
                    RemoveHandler _notifyCharacteristic.ValueChanged, AddressOf OnNotifyValueChanged
                Catch
                End Try
            End If
            _atEngineOpened = False
            _writeCharacteristic = Nothing
            _notifyCharacteristic = Nothing
            If _service IsNot Nothing Then _service.Dispose()
            If _device IsNot Nothing Then _device.Dispose()
            _service = Nothing
            _device = Nothing
        End Sub

        Private Shared Function FormatBluetoothAddress(address As ULong) As String
            Dim hex = address.ToString("X12")
            Return String.Join(":"c, Enumerable.Range(0, 6).Select(Function(i) hex.Substring(i * 2, 2)))
        End Function

        Private Shared Function BuildManufacturerHex(args As BluetoothLEAdvertisementReceivedEventArgs) As String
            Try
                If args.Advertisement.ManufacturerData Is Nothing OrElse args.Advertisement.ManufacturerData.Count = 0 Then Return ""
                Dim parts As New List(Of String)()
                For Each md In args.Advertisement.ManufacturerData
                    If md.Data.Length = 0UI Then Continue For
                    Dim bytes(CInt(md.Data.Length) - 1) As Byte
                    DataReader.FromBuffer(md.Data).ReadBytes(bytes)
                    parts.Add(md.CompanyId.ToString("X4") & ":" & BitConverter.ToString(bytes).Replace("-", ""))
                Next
                Return String.Join(";", parts)
            Catch
                Return ""
            End Try
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            StopScan()
            Disconnect()
        End Sub

    End Class

End Namespace
