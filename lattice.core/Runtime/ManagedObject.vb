Namespace Core
    Public Class ManagedObject
        Public Property TypeName As String
        Public Property Fields As Dictionary(Of String, Object)
        Public Property Methods As Dictionary(Of String, MethodDTO)
        Public ReadOnly Property Id As Guid

        Public Sub New(typeName As String)
            Me.TypeName = typeName
            Me.Fields = New Dictionary(Of String, Object)()
            Me.Methods = New Dictionary(Of String, MethodDTO)()
            Me.Id = Guid.NewGuid()
        End Sub

        Public Function GetField(name As String) As Object
            Dim val As Object = Nothing
            If Fields.TryGetValue(name, val) Then
                Return val
            End If
            Return Nothing
        End Function

        Public Sub SetField(name As String, value As Object)
            If Fields.ContainsKey(name) Then
                Fields(name) = value
            Else
                Fields.Add(name, value)
            End If
        End Sub

        Public Function HasMethod(name As String) As Boolean
            Return Methods.ContainsKey(name)
        End Function

        Public Function GetMethod(name As String) As MethodDTO
            If Methods.ContainsKey(name) Then
                Return Methods(name)
            End If
            Return Nothing
        End Function

        Public Overrides Function ToString() As String
            Return $"{TypeName}#{Id}"
        End Function
    End Class
End Namespace
