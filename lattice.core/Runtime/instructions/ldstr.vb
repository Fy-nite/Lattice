Imports lattice.Core

Namespace Runtime.instructions
    Public MustInherit Class ldstr
        Public name As String
        Public MustOverride Sub execute(callStack As CallStack)
    End Class
End Namespace