Imports System.Text.Json

Namespace Core

    Public NotInheritable Class ModuleDTO
        Public name As String
        Public version As String
        Public metadata As JsonElement
        Public types As TypeDTO()
        Public methods As MethodDTO()
    End Class

    Public NotInheritable Class TypeDTO
        Public kind As String
        Public name As String
        Public _namespace As String
        Public access As String
        Public isAbstract As Boolean
        Public isSealed As Boolean
        Public baseType As String
        Public attributes As List(Of AttributeDTO)
        Public fields As List(Of FieldDTO)
        Public methods As List(Of MethodDTO)
        Public interfaces As List(Of String)
    End Class
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