Imports lattice.Core

Namespace Runtime
    Public Interface Instruction
        Sub execute(ByVal callStack As CallStack)
    End Interface
End Namespace