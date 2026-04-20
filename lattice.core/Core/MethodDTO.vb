Namespace Core
    Public NotInheritable Class MethodDTO
        Public name As String
        Public ReturnType As String
        Public Parameters As List(Of ParameterDTO)
    End Class
    Public NotInheritable Class ParameterDTO
        Public name As String
        Public Type As String
    End Class
    Public NotInheritable Class LocalVariableDTO
        Public name As String
        Public Type As String
    End Class
    Public NotInheritable Class FieldDTO
        Public name As String
        Public Type As String
        Public access As String
        Public isStatic As Boolean
        Public isReadOnly As Boolean
        Public attributes As List(Of AttributeDTO)
    End Class
    Public NotInheritable Class AttributeDTO
        Public type As String
        Public constructorArguments As List(Of Object)
    End Class
End Namespace