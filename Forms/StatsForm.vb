Option Strict Off
Option Explicit On

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NightOutAdmin.Controls
Imports NightOutAdmin.Models
Imports NightOutAdmin.Services
Imports NightOutAdmin.Theme

Namespace Forms

    ''' <summary>Tableau de bord : cartes de chiffres-clés + graphiques.</summary>
    Public Class StatsForm
        Inherits Form
        Implements IRefreshable

        Private ReadOnly lblTitle As New Label()
        Private ReadOnly flowCards As New FlowLayoutPanel()
        Private ReadOnly chartTop As New BarChartControl()
        Private ReadOnly chartStatus As New DonutChartControl()
        Private ReadOnly chartCats As New BarChartControl()
        Private ReadOnly pnlScroll As New Panel()

        Public Sub New()
            Me.Text = "Statistiques"
            Me.BackColor = NightOutTheme.BgDark

            lblTitle.Text = "📊  Tableau de bord"
            lblTitle.ForeColor = NightOutTheme.Gold
            lblTitle.Font = NightOutTheme.FontTitle(14.0F)
            lblTitle.Dock = DockStyle.Top
            lblTitle.Height = 50
            lblTitle.TextAlign = ContentAlignment.MiddleLeft
            lblTitle.Padding = New Padding(16, 0, 0, 0)
            lblTitle.BackColor = NightOutTheme.BgPanel

            pnlScroll.Dock = DockStyle.Fill
            pnlScroll.AutoScroll = True
            pnlScroll.BackColor = NightOutTheme.BgDark
            pnlScroll.Padding = New Padding(16)

            ' Cartes
            flowCards.Location = New Point(16, 12)
            flowCards.Size = New Size(1120, 470)
            flowCards.AutoSize = False
            flowCards.FlowDirection = FlowDirection.LeftToRight
            flowCards.WrapContents = True
            flowCards.BackColor = NightOutTheme.BgDark

            ' Graphiques
            chartTop.TitleText = "Top établissements (abonnés)"
            chartTop.BarColor = NightOutTheme.Gold
            chartTop.Size = New Size(540, 260)
            chartTop.Location = New Point(16, 494)

            chartStatus.TitleText = "Bars par statut"
            chartStatus.Size = New Size(540, 260)
            chartStatus.Location = New Point(576, 494)

            chartCats.TitleText = "Bars par catégorie"
            chartCats.BarColor = NightOutTheme.Pink
            chartCats.Size = New Size(540, 280)
            chartCats.Location = New Point(16, 774)

            pnlScroll.Controls.Add(flowCards)
            pnlScroll.Controls.Add(chartTop)
            pnlScroll.Controls.Add(chartStatus)
            pnlScroll.Controls.Add(chartCats)

            Me.Controls.Add(pnlScroll)
            Me.Controls.Add(lblTitle)
        End Sub

        Private Async Sub StatsForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
            Await RefreshDataAsync()
        End Sub

        Public Async Function RefreshDataAsync() As Task Implements IRefreshable.RefreshDataAsync
            Try
                Me.UseWaitCursor = True
                lblTitle.Text = "📊  Tableau de bord — chargement…"
                Dim s = Await StatsService.GetDashboardAsync()

                flowCards.Controls.Clear()
                flowCards.Controls.Add(MakeCard("Bars (total)", s.BarsTotal, NightOutTheme.Gold))
                flowCards.Controls.Add(MakeCard("Bars validés", s.BarsApproved, NightOutTheme.Green))
                flowCards.Controls.Add(MakeCard("Bars à valider", s.BarsPending, NightOutTheme.Orange))
                flowCards.Controls.Add(MakeCard("Événements", s.EventsTotal, NightOutTheme.Pink))
                flowCards.Controls.Add(MakeCard("Événements en cours", s.EventsLive, NightOutTheme.Green))
                flowCards.Controls.Add(MakeCard("Événements à venir", s.EventsUpcoming, NightOutTheme.Blue))
                flowCards.Controls.Add(MakeCard("Comptes pro", s.ProTotal, NightOutTheme.Cream))
                flowCards.Controls.Add(MakeCard("Pro à valider", s.ProPending, NightOutTheme.Orange))
                flowCards.Controls.Add(MakeCard("Utilisateurs", s.UsersTotal, NightOutTheme.Blue))
                flowCards.Controls.Add(MakeCard("Utilisatrices", s.UsersFemale, NightOutTheme.Pink))
                flowCards.Controls.Add(MakeCard("Utilisateurs H", s.UsersMale, NightOutTheme.Blue))
                flowCards.Controls.Add(MakeCard("Genre non renseigné", s.UsersGenderUnknown, NightOutTheme.Muted))
                flowCards.Controls.Add(MakeCard("Abonnements bars", s.FollowersTotal, NightOutTheme.Gold))
                flowCards.Controls.Add(MakeCard("Check-ins", s.CheckinsTotal, NightOutTheme.Green))
                flowCards.Controls.Add(MakeCard("Check-ins filles", s.CheckinsFemale, NightOutTheme.Pink))
                flowCards.Controls.Add(MakeCard("Check-ins garçons", s.CheckinsMale, NightOutTheme.Blue))
                flowCards.Controls.Add(MakeCard("Check-ins non renseignés", s.CheckinsGenderUnknown, NightOutTheme.Muted))
                flowCards.Controls.Add(MakeCard("Vues fiches bars", s.BarProfileViewsTotal, NightOutTheme.Cream))

                chartTop.SetData(s.TopBars)
                chartCats.SetData(s.BarsByCategory)

                Dim slices As New List(Of (Label As String, Value As Integer, Col As Color)) From {
                    ("Validés", s.BarsApproved, NightOutTheme.Green),
                    ("En attente", s.BarsPending, NightOutTheme.Orange),
                    ("Refusés", s.BarsRejected, NightOutTheme.Red)
                }
                chartStatus.SetSlices(slices)

                lblTitle.Text = "📊  Tableau de bord"
            Catch ex As Exception
                lblTitle.Text = "📊  Tableau de bord"
                MessageBox.Show("Erreur de chargement des stats : " & ex.Message, "Statistiques",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.UseWaitCursor = False
            End Try
        End Function

        ''' <summary>Crée une carte chiffre-clé.</summary>
        Private Function MakeCard(title As String, value As Integer, accent As Color) As Panel
            Dim card As New Panel() With {
                .Size = New Size(200, 96),
                .Margin = New Padding(8),
                .BackColor = NightOutTheme.BgPanel
            }
            ' Liseré coloré à gauche
            Dim stripe As New Panel() With {.Dock = DockStyle.Left, .Width = 5, .BackColor = accent}
            card.Controls.Add(stripe)

            Dim lblValue As New Label() With {
                .Text = value.ToString("N0"),
                .ForeColor = accent,
                .Font = NightOutTheme.FontTitle(26.0F),
                .AutoSize = False,
                .Location = New Point(16, 12),
                .Size = New Size(176, 44)
            }
            Dim lblCaption As New Label() With {
                .Text = title,
                .ForeColor = NightOutTheme.Muted,
                .Font = NightOutTheme.FontBody(9.0F),
                .AutoSize = False,
                .Location = New Point(18, 60),
                .Size = New Size(176, 26)
            }
            card.Controls.Add(lblValue)
            card.Controls.Add(lblCaption)
            Return card
        End Function

    End Class

End Namespace
