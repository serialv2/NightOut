Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    ''' <summary>Moderation des evenements officiels et ephemeres.</summary>
    Public Class EventsForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly lblTitle As New Label()
        Private ReadOnly grid As New DataGridView()
        Private ReadOnly pnlActions As New Panel()
        Private ReadOnly btnStats As New Button()
        Private ReadOnly btnPublish As New Button()
        Private ReadOnly btnCancel As New Button()
        Private ReadOnly btnDelete As New Button()
        Private _view As New List(Of AdminEventRow)()

        Public Sub New()
            Me.Text = "Evenements"
            Me.BackColor = NightOutTheme.BgDark

            lblTitle.Text = "Evenements"
            lblTitle.ForeColor = NightOutTheme.Pink
            lblTitle.Font = NightOutTheme.FontTitle(13.0F)
            lblTitle.Dock = DockStyle.Top
            lblTitle.Height = 46
            lblTitle.TextAlign = ContentAlignment.MiddleLeft
            lblTitle.Padding = New Padding(14, 0, 0, 0)
            lblTitle.BackColor = NightOutTheme.BgPanel

            pnlActions.Dock = DockStyle.Bottom
            pnlActions.Height = 56
            pnlActions.BackColor = NightOutTheme.BgPanel
            pnlActions.Padding = New Padding(14, 10, 14, 10)

            NightOutTheme.StylePrimaryButton(btnStats, NightOutTheme.Pink)
            btnStats.Text = "Voir stats"
            btnStats.Width = 150
            btnStats.Dock = DockStyle.Left
            btnStats.Enabled = False

            Dim spStats As New Panel With {.Width = 10, .Dock = DockStyle.Left}

            NightOutTheme.StyleGhostButton(btnPublish, NightOutTheme.Green)
            btnPublish.Text = "Publier"
            btnPublish.Width = 150
            btnPublish.Dock = DockStyle.Left
            btnPublish.Enabled = False

            Dim sp As New Panel With {.Width = 10, .Dock = DockStyle.Left}

            NightOutTheme.StyleGhostButton(btnCancel, NightOutTheme.Orange)
            btnCancel.Text = "Annuler"
            btnCancel.Width = 150
            btnCancel.Dock = DockStyle.Left
            btnCancel.Enabled = False

            NightOutTheme.StyleGhostButton(btnDelete, NightOutTheme.Red)
            btnDelete.Text = "Supprimer"
            btnDelete.Width = 150
            btnDelete.Dock = DockStyle.Right
            btnDelete.Enabled = False

            pnlActions.Controls.Add(btnPublish)
            pnlActions.Controls.Add(sp)
            pnlActions.Controls.Add(btnCancel)
            pnlActions.Controls.Add(spStats)
            pnlActions.Controls.Add(btnStats)
            pnlActions.Controls.Add(btnDelete)

            NightOutTheme.StyleGrid(grid)
            grid.Dock = DockStyle.Fill
            grid.Columns.Add(NewCol("Type", "Type"))
            grid.Columns.Add(NewCol("Title", "Titre"))
            grid.Columns.Add(NewCol("Place", "Etablissement / lieu"))
            grid.Columns.Add(NewCol("Creator", "Createur"))
            grid.Columns.Add(NewCol("Date", "Debut"))
            grid.Columns.Add(NewCol("State", "Etat"))
            grid.Columns.Add(NewCol("Status", "Statut"))
            grid.Columns("Type").FillWeight = 42
            grid.Columns("Date").FillWeight = 70
            grid.Columns("State").FillWeight = 55
            grid.Columns("Status").FillWeight = 55

            Me.Controls.Add(grid)
            Me.Controls.Add(pnlActions)
            Me.Controls.Add(lblTitle)

            AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
            AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
            AddHandler btnStats.Click, AddressOf Stats_Click
            AddHandler btnPublish.Click, AddressOf Publish_Click
            AddHandler btnCancel.Click, AddressOf Cancel_Click
            AddHandler btnDelete.Click, AddressOf Delete_Click
        End Sub

        Private Shared Function NewCol(name As String, header As String) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header}
        End Function

        Private Async Sub EventsForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Me.UseWaitCursor = True

                Dim official = Await EventService.GetAllAsync()
                Dim ephemeral = Await EventService.GetEphemeralAllAsync()

                _view = official.Select(Function(ev) AdminEventRow.FromOfficial(ev)).
                    Concat(ephemeral.Select(Function(ev) AdminEventRow.FromEphemeral(ev))).
                    OrderByDescending(Function(ev) ev.StartAt).
                    ToList()

                grid.Rows.Clear()
                For Each ev In _view
                    grid.Rows.Add(ev.TypeLabel, ev.Title, ev.PlaceLabel, ev.CreatorLabel, ev.DateLabel, ev.StateLabel, ev.StatusLabel)
                Next

                lblTitle.Text = $"Evenements ({_view.Count}) - {official.Count} officiel(s) - {ephemeral.Count} ephemere(s)"
            Catch ex As Exception
                MessageBox.Show("Erreur : " & ex.Message, "Evenements", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Private Function Selected() As AdminEventRow
            If grid.CurrentRow Is Nothing Then Return Nothing
            Dim idx = grid.CurrentRow.Index
            If idx < 0 OrElse idx >= _view.Count Then Return Nothing
            Return _view(idx)
        End Function

        Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
            Dim has = (Selected() IsNot Nothing)
            btnStats.Enabled = has
            btnPublish.Enabled = has
            btnCancel.Enabled = has
            btnDelete.Enabled = has
        End Sub

        Private Sub Stats_Click(sender As Object, e As EventArgs)
            OpenStats()
        End Sub

        Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            grid.CurrentCell = grid.Rows(e.RowIndex).Cells(0)
            OpenStats()
        End Sub

        Private Sub OpenStats()
            Dim ev = Selected()
            If ev Is Nothing Then Return

            Using f As New EventActivityForm(ev)
                f.ShowDialog(Me)
            End Using
        End Sub

        Private Async Sub Publish_Click(sender As Object, e As EventArgs)
            Dim ev = Selected() : If ev Is Nothing Then Return
            Await DoAction(Function() ev.PublishAsync())
        End Sub

        Private Async Sub Cancel_Click(sender As Object, e As EventArgs)
            Dim ev = Selected() : If ev Is Nothing Then Return
            If MessageBox.Show($"Annuler ""{ev.Title}"" ?", "Confirmation",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            Await DoAction(Function() ev.CancelAsync())
        End Sub

        Private Async Sub Delete_Click(sender As Object, e As EventArgs)
            Dim ev = Selected() : If ev Is Nothing Then Return
            If MessageBox.Show($"Supprimer ""{ev.Title}"" ?" & vbCrLf & "Action irreversible.",
                "Suppression", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
            Await DoAction(Function() ev.DeleteAsync())
        End Sub

        Private Async Function DoAction(action As Func(Of Task(Of Boolean))) As Task
            Try
                Me.UseWaitCursor = True
                Await action()
                Await RefreshDataAsync()
            Catch ex As Exception
                MessageBox.Show("Echec : " & ex.Message, "Action", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        Public Class AdminEventRow
            Public Property Kind As String
            Public Property Official As OfficialEvent
            Public Property Ephemeral As EphemeralEvent

            Public ReadOnly Property Id As String
                Get
                    If Official IsNot Nothing Then Return Official.Id
                    Return Ephemeral?.Id
                End Get
            End Property

            Public ReadOnly Property Title As String
                Get
                    If Official IsNot Nothing Then Return If(Official.Title, "Evenement")
                    Return If(Ephemeral?.Title, "Sortie ephemere")
                End Get
            End Property

            Public ReadOnly Property TypeLabel As String
                Get
                    Return If(Kind = "official", "Officiel", "Ephemere")
                End Get
            End Property

            Public ReadOnly Property PlaceLabel As String
                Get
                    If Official IsNot Nothing Then Return If(Official.BarName, "-")
                    If Not String.IsNullOrWhiteSpace(Ephemeral?.BarName) Then Return Ephemeral.BarName
                    If Not String.IsNullOrWhiteSpace(Ephemeral?.PlaceName) Then Return Ephemeral.PlaceName
                    If Not String.IsNullOrWhiteSpace(Ephemeral?.Address) Then Return Ephemeral.Address
                    Return "-"
                End Get
            End Property

            Public ReadOnly Property CreatorLabel As String
                Get
                    If Official IsNot Nothing Then Return If(Official.ProfessionalAccountId, "Compte pro")
                    Return If(Ephemeral?.CreatorName, If(Ephemeral?.CreatorId, "Utilisateur"))
                End Get
            End Property

            Public ReadOnly Property StartAt As DateTime
                Get
                    If Official IsNot Nothing Then Return Official.StartAt
                    Return If(Ephemeral IsNot Nothing, Ephemeral.StartAt, DateTime.MinValue)
                End Get
            End Property

            Public ReadOnly Property DateLabel As String
                Get
                    If Official IsNot Nothing Then Return Official.DateLabel
                    Return If(Ephemeral?.DateLabel, "-")
                End Get
            End Property

            Public ReadOnly Property StateLabel As String
                Get
                    Dim now = DateTime.UtcNow
                    Dim endsAt = If(Official IsNot Nothing, Official.EffectiveEnd, If(Ephemeral IsNot Nothing, Ephemeral.ExpiresAt, DateTime.MinValue))
                    If StartAt.ToUniversalTime() <= now AndAlso endsAt.ToUniversalTime() >= now Then Return "En cours"
                    If StartAt.ToUniversalTime() > now Then Return "A venir"
                    Return "Termine"
                End Get
            End Property

            Public ReadOnly Property StatusLabel As String
                Get
                    If Official IsNot Nothing Then Return Official.StatusLabel
                    Return If(Ephemeral?.StatusLabel, "-")
                End Get
            End Property

            Public Shared Function FromOfficial(ev As OfficialEvent) As AdminEventRow
                Return New AdminEventRow With {.Kind = "official", .Official = ev}
            End Function

            Public Shared Function FromEphemeral(ev As EphemeralEvent) As AdminEventRow
                Return New AdminEventRow With {.Kind = "ephemeral", .Ephemeral = ev}
            End Function

            Public Function PublishAsync() As Task(Of Boolean)
                If Kind = "official" Then Return EventService.PublishAsync(Id)
                Return EventService.PublishEphemeralAsync(Id)
            End Function

            Public Function CancelAsync() As Task(Of Boolean)
                If Kind = "official" Then Return EventService.CancelAsync(Id)
                Return EventService.CancelEphemeralAsync(Id)
            End Function

            Public Function DeleteAsync() As Task(Of Boolean)
                If Kind = "official" Then Return EventService.DeleteAsync(Id)
                Return EventService.DeleteEphemeralAsync(Id)
            End Function
        End Class

    End Class

End Namespace
