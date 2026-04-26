Namespace Throwables
    Public Class CPUInstructionException
        Inherits LatticeException
        Public Sub New(message As String, Optional errorCode As String = "C001", Optional helpText As String = "", Optional location As String = "", Optional notes As IEnumerable(Of String) = Nothing)
            MyBase.New(message, errorCode, helpText, location, notes)
        End Sub
    End Class
End Namespace