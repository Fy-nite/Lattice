Namespace Throwables
    Public Class RuntimeException
        Inherits LatticeException
        Public Sub New(message As String, Optional errorCode As String = "R001", Optional helpText As String = "", Optional location As String = "", Optional notes As IEnumerable(Of String) = Nothing)
            MyBase.New(message, errorCode, helpText, location, notes)
        End Sub
    End Class
End Namespace