Option Strict Off
Option Explicit On

Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports NightOutAdmin.Config

Namespace Services

    ''' <summary>
    ''' Client HTTP Supabase partagé (singleton statique).
    ''' Gère l'authentification GoTrue puis les appels REST/PostgREST
    ''' avec le JWT de l'utilisateur connecté (les politiques RLS s'appliquent).
    ''' </summary>
    Public Module SupabaseClient

        Private ReadOnly _http As New HttpClient()
        Public Property AccessToken As String = String.Empty
        Public Property CurrentUserId As String = String.Empty

        ''' <summary>Authentifie via email/mot de passe. Retourne (ok, message, userId).</summary>
        Public Async Function SignInAsync(email As String, password As String) _
            As Task(Of (Ok As Boolean, Message As String, UserId As String))

            Try
                Dim url = SupabaseConfig.AuthUrl & "token?grant_type=password"
                Dim payload = New JObject(
                    New JProperty("email", email),
                    New JProperty("password", password))

                Using req As New HttpRequestMessage(HttpMethod.Post, url)
                    req.Headers.TryAddWithoutValidation("apikey", SupabaseConfig.AnonKey)
                    req.Content = New StringContent(payload.ToString(), Encoding.UTF8, "application/json")

                    Dim resp = Await _http.SendAsync(req)
                    Dim body = Await resp.Content.ReadAsStringAsync()

                    If Not resp.IsSuccessStatusCode Then
                        Dim msg = "Identifiants invalides."
                        Try
                            Dim err = JObject.Parse(body)
                            If err("error_description") IsNot Nothing Then
                                msg = err("error_description").ToString()
                            ElseIf err("msg") IsNot Nothing Then
                                msg = err("msg").ToString()
                            End If
                        Catch
                        End Try
                        Return (False, msg, Nothing)
                    End If

                    Dim json = JObject.Parse(body)
                    AccessToken = json("access_token").ToString()
                    CurrentUserId = json("user")("id").ToString()
                    Return (True, "OK", CurrentUserId)
                End Using

            Catch ex As Exception
                Return (False, "Erreur réseau : " & ex.Message, Nothing)
            End Try
        End Function

        Public Sub SignOut()
            AccessToken = String.Empty
            CurrentUserId = String.Empty
        End Sub

        Private Sub AddAuthHeaders(req As HttpRequestMessage)
            req.Headers.TryAddWithoutValidation("apikey", SupabaseConfig.AnonKey)
            If Not String.IsNullOrEmpty(AccessToken) Then
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " & AccessToken)
            End If
        End Sub

        ''' <summary>
        ''' GET PostgREST. <paramref name="query"/> est le chemin après /rest/v1/
        ''' (ex : "bars?select=*&order=created_at.desc").
        ''' </summary>
        Public Async Function GetListAsync(Of T)(query As String) As Task(Of List(Of T))
            Dim url = SupabaseConfig.RestUrl & query
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                AddAuthHeaders(req)
                Dim resp = Await _http.SendAsync(req)
                Dim body = Await resp.Content.ReadAsStringAsync()
                If Not resp.IsSuccessStatusCode Then
                    Throw New Exception($"GET {query} → {CInt(resp.StatusCode)} : {body}")
                End If
                Dim result = JsonConvert.DeserializeObject(Of List(Of T))(body)
                Return If(result, New List(Of T)())
            End Using
        End Function

        ''' <summary>Renvoie le JSON brut (utile pour parsing manuel/JArray).</summary>
        Public Async Function GetRawAsync(query As String) As Task(Of String)
            Dim url = SupabaseConfig.RestUrl & query
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                AddAuthHeaders(req)
                Dim resp = Await _http.SendAsync(req)
                Dim body = Await resp.Content.ReadAsStringAsync()
                If Not resp.IsSuccessStatusCode Then
                    Throw New Exception($"GET {query} → {CInt(resp.StatusCode)} : {body}")
                End If
                Return body
            End Using
        End Function

        ''' <summary>
        ''' Compte exact de lignes d'une table avec filtre optionnel,
        ''' via l'en-tête Content-Range (Prefer: count=exact).
        ''' <paramref name="filter"/> ex : "status=eq.pending".
        ''' </summary>
        Public Async Function CountAsync(table As String, Optional filter As String = "") As Task(Of Integer)
            Dim q = table & "?select=id"
            If Not String.IsNullOrEmpty(filter) Then q &= "&" & filter
            Dim url = SupabaseConfig.RestUrl & q
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                AddAuthHeaders(req)
                req.Headers.TryAddWithoutValidation("Prefer", "count=exact")
                req.Headers.Range = New System.Net.Http.Headers.RangeHeaderValue(0, 0)
                Dim resp = Await _http.SendAsync(req)
                If resp.Content.Headers.ContentRange IsNot Nothing AndAlso
                   resp.Content.Headers.ContentRange.Length.HasValue Then
                    Return CInt(resp.Content.Headers.ContentRange.Length.Value)
                End If
                ' Repli : on lit le Content-Range manuellement
                Dim cr As IEnumerable(Of String) = Nothing
                If resp.Content.Headers.TryGetValues("Content-Range", cr) Then
                    For Each v In cr
                        Dim slash = v.LastIndexOf("/"c)
                        If slash >= 0 Then
                            Dim totalStr = v.Substring(slash + 1)
                            Dim total As Integer
                            If Integer.TryParse(totalStr, total) Then Return total
                        End If
                    Next
                End If
                Return 0
            End Using
        End Function

        ''' <summary>PATCH (update) une table avec filtre PostgREST.</summary>
        Public Async Function PatchAsync(table As String, filter As String, body As JObject) As Task(Of Boolean)
            Dim url = SupabaseConfig.RestUrl & table & "?" & filter
            Using req As New HttpRequestMessage(New HttpMethod("PATCH"), url)
                AddAuthHeaders(req)
                req.Headers.TryAddWithoutValidation("Prefer", "return=minimal")
                req.Content = New StringContent(body.ToString(), Encoding.UTF8, "application/json")
                Dim resp = Await _http.SendAsync(req)
                If Not resp.IsSuccessStatusCode Then
                    Dim err = Await resp.Content.ReadAsStringAsync()
                    Throw New Exception($"PATCH {table} → {CInt(resp.StatusCode)} : {err}")
                End If
                Return True
            End Using
        End Function

        ''' <summary>
        ''' UPSERT (insert + merge sur conflit) d'un lot de lignes.
        ''' <paramref name="onConflict"/> ex : "bar_id,day_of_week".
        ''' </summary>
        Public Async Function UpsertAsync(table As String, rows As JArray, Optional onConflict As String = "") As Task(Of Boolean)
            Dim url = SupabaseConfig.RestUrl & table
            If Not String.IsNullOrEmpty(onConflict) Then url &= "?on_conflict=" & onConflict
            Using req As New HttpRequestMessage(HttpMethod.Post, url)
                AddAuthHeaders(req)
                req.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal")
                req.Content = New StringContent(rows.ToString(), Encoding.UTF8, "application/json")
                Dim resp = Await _http.SendAsync(req)
                If Not resp.IsSuccessStatusCode Then
                    Dim err = Await resp.Content.ReadAsStringAsync()
                    Throw New Exception($"UPSERT {table} → {CInt(resp.StatusCode)} : {err}")
                End If
                Return True
            End Using
        End Function

        ''' <summary>INSERT simple dans une table PostgREST.</summary>
        Public Async Function InsertAsync(table As String, body As JObject) As Task(Of Boolean)
            Dim url = SupabaseConfig.RestUrl & table
            Using req As New HttpRequestMessage(HttpMethod.Post, url)
                AddAuthHeaders(req)
                req.Headers.TryAddWithoutValidation("Prefer", "return=minimal")
                req.Content = New StringContent(body.ToString(), Encoding.UTF8, "application/json")
                Dim resp = Await _http.SendAsync(req)
                If Not resp.IsSuccessStatusCode Then
                    Dim err = Await resp.Content.ReadAsStringAsync()
                    Throw New Exception($"INSERT {table} → {CInt(resp.StatusCode)} : {err}")
                End If
                Return True
            End Using
        End Function

        ''' <summary>DELETE une table avec filtre PostgREST.</summary>
        Public Async Function DeleteAsync(table As String, filter As String) As Task(Of Boolean)
            Dim url = SupabaseConfig.RestUrl & table & "?" & filter
            Using req As New HttpRequestMessage(HttpMethod.Delete, url)
                AddAuthHeaders(req)
                req.Headers.TryAddWithoutValidation("Prefer", "return=minimal")
                Dim resp = Await _http.SendAsync(req)
                If Not resp.IsSuccessStatusCode Then
                    Dim err = Await resp.Content.ReadAsStringAsync()
                    Throw New Exception($"DELETE {table} → {CInt(resp.StatusCode)} : {err}")
                End If
                Return True
            End Using
        End Function


        ''' <summary>Appelle une fonction RPC Supabase (/rest/v1/rpc/nom_fonction).</summary>
        Public Async Function RpcAsync(functionName As String, body As JObject) As Task(Of Boolean)
            Dim url = SupabaseConfig.RestUrl & "rpc/" & functionName
            Using req As New HttpRequestMessage(HttpMethod.Post, url)
                AddAuthHeaders(req)
                req.Headers.TryAddWithoutValidation("Prefer", "return=minimal")
                req.Content = New StringContent(If(body, New JObject()).ToString(), Encoding.UTF8, "application/json")
                Dim resp = Await _http.SendAsync(req)
                If Not resp.IsSuccessStatusCode Then
                    Dim err = Await resp.Content.ReadAsStringAsync()
                    Throw New Exception($"RPC {functionName} → {CInt(resp.StatusCode)} : {err}")
                End If
                Return True
            End Using
        End Function

    End Module

End Namespace
