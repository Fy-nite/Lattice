Namespace Core
    Public Class CallStack
        Public Property Method As MethodDTO
        Public Property IP As Integer
        Public Property Locals As Dictionary(Of String, Object)
        Public Property Args As Dictionary(Of String, Object)
        Public Property This As ManagedObject
        Public Property Previous As CallStack

        Public Sub New(method As MethodDTO, Optional thisObj As ManagedObject = Nothing)
            Me.Method = method
            Me.IP = 0
            Me.Locals = New Dictionary(Of String, Object)()
            Me.Args = New Dictionary(Of String, Object)()
            Me.This = thisObj
            Me.Previous = Nothing
        End Sub

        Public Function PushFrame(newMethod As MethodDTO, Optional thisObj As ManagedObject = Nothing) As CallStack
            Dim frame As New CallStack(newMethod, thisObj)
            frame.Previous = Me
            Return frame
        End Function

        Public Function PopFrame() As CallStack
            Return Me.Previous
        End Function

        Public Overrides Function ToString() As String
            Dim name = If(Method IsNot Nothing AndAlso Method.name IsNot Nothing, Method.name, "unknown")
            Return $"{name} @ {IP}"
        End Function
    End Class
End Namespace