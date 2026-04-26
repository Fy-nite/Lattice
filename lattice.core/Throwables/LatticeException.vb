Namespace Throwables
    Public Class LatticeException
        Inherits Exception

        Public Property ErrorCode As String
        Public Property HelpText As String
        Public Property Location As String
        Public Property Notes As New List(Of String)()

        Public Sub New(message As String, Optional errorCode As String = "L000", Optional helpText As String = "", Optional location As String = "", Optional notes As IEnumerable(Of String) = Nothing)
            MyBase.New(message)
            Me.ErrorCode = errorCode
            Me.HelpText = helpText
            Me.Location = location
            If notes IsNot Nothing Then
                Me.Notes.AddRange(notes)
            End If
        End Sub

        Public Overrides ReadOnly Property Message As String
            Get
                Dim sb As New System.Text.StringBuilder()
                sb.AppendLine($"error[{ErrorCode}]: {MyBase.Message}")
                If Not String.IsNullOrEmpty(Location) Then
                    sb.AppendLine($"  --> {Location}")
                End If

                For Each n In Notes
                    sb.AppendLine($"  = note: {n}")
                Next

                If Not String.IsNullOrEmpty(HelpText) Then
                    sb.AppendLine()
                    sb.AppendLine($"  = help: {HelpText}")
                End If
                Return sb.ToString()
            End Get
        End Property
    End Class
End Namespace
