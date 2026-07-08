Option Strict Off
Option Explicit On

Imports System.Drawing
Imports System.Windows.Forms

Namespace Theme

    ''' <summary>
    ''' Palette NightOut (reprise de Resources/Raw/map.html de l'app MAUI)
    ''' et helpers de stylisation pour les contrôles WinForms.
    ''' </summary>
    Public Module NightOutTheme

        ' ── Fonds ──
        Public ReadOnly BgDark As Color = ColorTranslator.FromHtml("#0A1018")
        Public ReadOnly BgPanel As Color = ColorTranslator.FromHtml("#0F1923")
        Public ReadOnly BgPanel2 As Color = ColorTranslator.FromHtml("#131F2B")
        Public ReadOnly BgPanel3 As Color = ColorTranslator.FromHtml("#1A2A38")

        ' ── Accents ──
        Public ReadOnly Gold As Color = ColorTranslator.FromHtml("#FFB627")
        Public ReadOnly Orange As Color = ColorTranslator.FromHtml("#FF6B35")
        Public ReadOnly Green As Color = ColorTranslator.FromHtml("#3DB87A")
        Public ReadOnly Pink As Color = ColorTranslator.FromHtml("#E5559F")
        Public ReadOnly Red As Color = ColorTranslator.FromHtml("#E5484D")
        Public ReadOnly Blue As Color = ColorTranslator.FromHtml("#4F9DF7")

        ' ── Textes ──
        Public ReadOnly Cream As Color = ColorTranslator.FromHtml("#F2E8D5")
        Public ReadOnly Muted As Color = ColorTranslator.FromHtml("#7A8FA6")
        Public ReadOnly Border As Color = ColorTranslator.FromHtml("#23323F")

        Public Function FontTitle(size As Single) As Font
            Return New Font("Segoe UI Semibold", size, FontStyle.Bold)
        End Function

        Public Function FontBody(size As Single) As Font
            Return New Font("Segoe UI", size, FontStyle.Regular)
        End Function

        ''' <summary>Applique le thème sombre à un bouton "plat".</summary>
        Public Sub StylePrimaryButton(btn As Button, Optional accent As Color = Nothing)
            If accent = Nothing OrElse accent.IsEmpty Then accent = Gold
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 0
            btn.BackColor = accent
            btn.ForeColor = BgDark
            btn.Font = FontTitle(9.5F)
            btn.Cursor = Cursors.Hand
            btn.Height = 36
        End Sub

        Public Sub StyleGhostButton(btn As Button, Optional accent As Color = Nothing)
            If accent = Nothing OrElse accent.IsEmpty Then accent = Gold
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 1
            btn.FlatAppearance.BorderColor = accent
            btn.BackColor = BgPanel2
            btn.ForeColor = accent
            btn.Font = FontTitle(9.5F)
            btn.Cursor = Cursors.Hand
            btn.Height = 36
        End Sub

        ''' <summary>Applique le thème sombre à un DataGridView.</summary>
        Public Sub StyleGrid(grid As DataGridView)
            grid.EnableHeadersVisualStyles = False
            grid.BackgroundColor = BgPanel
            grid.BorderStyle = BorderStyle.None
            grid.GridColor = Border
            grid.Font = FontBody(9.0F)
            grid.RowHeadersVisible = False
            grid.AllowUserToAddRows = False
            grid.AllowUserToDeleteRows = False
            grid.ReadOnly = True
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            grid.MultiSelect = False
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            grid.RowTemplate.Height = 34

            grid.ColumnHeadersDefaultCellStyle.BackColor = BgPanel2
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Gold
            grid.ColumnHeadersDefaultCellStyle.Font = FontTitle(9.0F)
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft
            grid.ColumnHeadersHeight = 38
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None

            grid.DefaultCellStyle.BackColor = BgPanel
            grid.DefaultCellStyle.ForeColor = Cream
            grid.DefaultCellStyle.SelectionBackColor = BgPanel3
            grid.DefaultCellStyle.SelectionForeColor = Gold
            grid.DefaultCellStyle.Padding = New Padding(6, 0, 6, 0)

            grid.AlternatingRowsDefaultCellStyle.BackColor = BgPanel2
        End Sub

    End Module

End Namespace
