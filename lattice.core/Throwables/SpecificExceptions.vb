Namespace Throwables
    Public Class OpCodeNotFoundException
        Inherits RuntimeException
        Public Sub New(opCode As String, location As String)
            MyBase.New($"Unknown opcode: {opCode}", "R002", $"The opcode '{opCode}' is not supported by the current CPU implementation. Check for typos or unsupported instructions.", location)
        End Sub
    End Class

    Public Class MethodResolutionException
        Inherits RuntimeException
        Public Sub New(methodName As String, location As String)
            MyBase.New($"Could not resolve method: {methodName}", "R003", $"Ensure that the method '{methodName}' is defined and accessible.", location)
        End Sub
    End Class

    Public Class EntrypointNotFoundException
        Inherits RuntimeException
        Public Sub New(message As String, help As String)
            MyBase.New(message, "E001", help)
        End Sub
    End Class
End Namespace
