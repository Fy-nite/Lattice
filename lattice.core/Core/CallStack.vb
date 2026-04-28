Imports Humanizer
Imports ObjectIR.Core.AST

Namespace Core
    Public Class CallStack
        Public Property Method As MethodNode
        Public Property IP As Integer
        Public Property Locals As Dictionary(Of String, Object)
        Public Property Args As Dictionary(Of String, Object)
        Public Property This As ManagedObject
        Public Property Previous As CallStack
        Public Property EvaluationStack As Stack(Of Object)

        Public Sub New(method As MethodNode, Optional thisObj As ManagedObject = Nothing)
            Me.Method = method
            Me.IP = 0
            Me.Locals = New Dictionary(Of String, Object)()
            Me.Args = New Dictionary(Of String, Object)()
            Me.This = thisObj
            Me.Previous = Nothing
            Me.EvaluationStack = New Stack(Of Object)()
        End Sub

        Public Function PushFrame(newMethod As MethodNode, Optional thisObj As ManagedObject = Nothing) As CallStack
            Dim frame As New CallStack(newMethod, thisObj)
            frame.Previous = Me
            Return frame
        End Function

        Public Function PopFrame() As CallStack
            Return Me.Previous
        End Function

        Public Overrides Function ToString() As String
            Dim name = If(Method IsNot Nothing AndAlso Method.Name IsNot Nothing, Method.Name, "unknown")
            Return $"at {name} @ {IP} with args {Me.Args.Humanize()}"
            'Return GetStackTrace()
        End Function

        Public Function GetStackTrace() As String
            Dim sb As New System.Text.StringBuilder()
            Dim current As CallStack = Me
            While current IsNot Nothing
                sb.AppendLine(current.ToString())
                current = current.Previous
            End While
            Return sb.ToString()
        End Function
    End Class
End Namespace