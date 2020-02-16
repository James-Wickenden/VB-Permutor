﻿Public Class PermutationLibrary(Of T)
    'PermutationLibrary version 1.7 (16/02/2020)
    'A Permutation library by James https://github.com/James-Wickenden/VB-Permutor
    'Provides framework to allow generic permuting of arrays, either with or without repetition.
    'The permutator can handle up to 255 possible values when streaming.

    'TODO:
    '   Wrong typed input validation
    '   Parameter Guidelines
    '   Config as DLL (?)
    '   Make streamPermutor() private!
    '   Clean Up Permutor Interface; Public Subs and Method Formatting

    Private sizeOfPermutation As Integer
    Private possibleValues() As T
    Private possibleValueIndices As List(Of Integer)
    Private allowDuplicates As Boolean
    Private streamHandler As StreamHandler(Of T)

    '/////////////////////////
    'These methods relate to initialisation and configuration of permutor attributes.
    '/////////////////////////

    'Initialise the attributes that classify fully an instance of the permutor.
    Public Sub New(ByVal sizeOfPermutation As Integer, ByVal possibleValues() As T, ByVal allowDuplicates As Boolean)
        possibleValueIndices = New List(Of Integer)
        configure(sizeOfPermutation, possibleValues, allowDuplicates)
    End Sub

    Public Sub configure(ByVal sizeOfPermutation As Integer, ByVal possibleValues() As T, ByVal allowDuplicates As Boolean)
        Me.sizeOfPermutation = sizeOfPermutation
        Me.possibleValues = possibleValues
        Me.allowDuplicates = allowDuplicates
        configIndicesList()
    End Sub

    Public Sub validate(fromList As Boolean)
        Dim exceptionStr As String = ""
        If possibleValues.Count = 0 Then exceptionStr &=
            "ERROR: [possibleValues] attribute must contain elements. "
        If possibleValues.Count >= 255 Then exceptionStr &=
            "ERROR: [possibleValues] attribute contains too many elements. "
        If possibleValueIndices.Count = 0 Then exceptionStr &=
            "ERROR: [possibleValueIndices] attribute must contain elements. Should be configured by changing the [possibleValues] attribute. "
        If possibleValueIndices.Count >= 255 Then exceptionStr &=
            "ERROR: [possibleValueIndices] attribute contains too many elements. Should be configured by changing the [possibleValues] attribute. "

        If sizeOfPermutation < 0 Then exceptionStr &=
            "ERROR: [sizeOfPermutation] attribute must be a positive integer. "
        If sizeOfPermutation > possibleValues.Count And allowDuplicates = False Or
           sizeOfPermutation > possibleValueIndices.Count And allowDuplicates = False Then exceptionStr &=
            "ERROR: [sizeOfPermutation] attribute must be lower than magnitude of [possibleValues] if duplicate elements are not allowed. "

        If fromList = True Then
            Dim noPerms As Long = getNoOfPermutations()
            If noPerms = -1 Or noPerms >= Math.Pow(2, 28) Then exceptionStr &= "ERROR: Too many permutations generated."
        End If

        If exceptionStr <> "" Then Throw New Exception(exceptionStr)
    End Sub

    'Sets up the indices list which is used for permuting corresponding integer indices instead of objects of type T.
    Private Sub configIndicesList()
        Dim x As Integer = 0
        possibleValueIndices.Clear()
        For Each elem As T In possibleValues
            possibleValueIndices.Add(x)
            x += 1
        Next
    End Sub

    'Establishes the first permutation as an array size [sizeOfPermutation], with each element as [possibleValues.First]
    Private Function initPermutingArray() As List(Of Integer)
        Dim permutingList As New List(Of Integer)
        For i As Integer = 0 To sizeOfPermutation - 1
            permutingList.Add(possibleValueIndices.First)
        Next
        Return permutingList
    End Function

    'Getters and setters for the permutor attributes. Not used in the code but added here for the user.
    Public Sub setSizeOfPermutation(newPermutationSize As Integer)
        sizeOfPermutation = newPermutationSize
    End Sub

    Public Sub setPossibleValues(newPossibleValues As T())
        possibleValues = newPossibleValues
        configIndicesList()
    End Sub

    Public Sub setAllowDuplicates(newAllowDuplicates As Boolean)
        allowDuplicates = newAllowDuplicates
    End Sub

    Public Function getSizeOfPermutation() As Integer
        Return sizeOfPermutation
    End Function

    Public Function getPossibleValues() As T()
        Return possibleValues.ToArray
    End Function

    Public Function getAllowDuplicates()
        Return allowDuplicates
    End Function

    '/////////////////////////
    'These methods provide basic mathematical and logical functionality.
    '/////////////////////////

    Private Function factorial(x As Long) As Long
        If x <= 1 Then Return 1
        Return x * factorial(x - 1)
    End Function

    Private Sub swap(ByRef x As Integer, ByRef y As Integer)
        Dim temp As Integer = x
        x = y
        y = temp
    End Sub

    '/////////////////////////
    'These methods provide logic functions that together generate permuations.
    '/////////////////////////

    'Used to determine when the final permutation has been calculated
    'The final permutation is reached when each element in [permutee] is the final value in [possibleValues]
    Private Function permuteeContainsOnlyFinalElement(ByVal permutee As List(Of Integer)) As Boolean
        If permutee.All(Function(elem) elem.Equals(possibleValueIndices.Last)) Then
            Return True
        End If
        Return False
    End Function

    'Finds the next value for the [index] element of [permutee]
    'The next element loops around to the first if [permutee(index)] is [possibleValues.Last]
    'In this case, the [toNextColumn] flag is set to True.
    Private Sub getNextPossibleColummValue(ByRef permutee As List(Of Integer), ByRef toNextColumn As Boolean, ByVal index As Integer)
        If possibleValueIndices.Last.Equals(permutee(index)) Then
            permutee(index) = possibleValueIndices.First
            toNextColumn = True
        Else
            permutee(index) = possibleValueIndices.ElementAt(possibleValueIndices.IndexOf(permutee(index)) + 1)
        End If
    End Sub

    'Generates the next permutation by counting up in base [possibleValues.count] a number with [sizeOfPermutation] digits.
    'Each digit corresponds to the index of that element in possibleValues.
    'This generates every possible permutation with repetition.
    Private Sub findNextPermutation(ByRef permutee As List(Of Integer))
        Dim traversalIndex As Integer = 0
        Dim toNextColumn As Boolean = False

        getNextPossibleColummValue(permutee, toNextColumn, 0)
        While toNextColumn = True And traversalIndex < sizeOfPermutation - 1
            toNextColumn = False
            traversalIndex += 1
            getNextPossibleColummValue(permutee, toNextColumn, traversalIndex)
        End While
    End Sub

    'Streams the current permutation to the host thread securely using Semaphores, avoiding deadlock.
    'Note that the corresponding code must be used in the host thread to receive the streamed data, given in the exampleStreamReceiver() method.
    Private Sub outputHandler(ByVal permutee() As Integer, ByRef stream As System.IO.MemoryStream,
                              ByRef permutationAvle As Threading.Semaphore,
                              ByRef permutationPost As Threading.Semaphore,
                              ByRef permutationLock As Threading.Semaphore)
        If Not allowDuplicates And permutee.Distinct.Count <> permutee.Count Then Exit Sub
        Dim permuteeIndexBytes() As Byte = permuteeToBytes(permutee)

        permutationAvle.WaitOne()
        permutationLock.WaitOne()
        '/// CRITICAL SECTION START
        stream.Position = 0
        stream.Write(permuteeIndexBytes, 0, permuteeIndexBytes.Length)
        '/// CRITICAL SECTION END
        permutationPost.Release()
        permutationLock.Release()
    End Sub

    'Appends a new permutation to the results list by first generating the corresponding object permutation.
    Private Sub addResultToList(ByRef res As List(Of T()), ByVal permutee As List(Of Integer))
        Dim permuteeAsTs As New List(Of T)
        For Each elem As Integer In permutee
            permuteeAsTs.Add(possibleValues.ElementAt(elem))
        Next
        res.Add(permuteeAsTs.ToArray)
    End Sub

    'Converts the [permutee] to an array of Bytes to allow transmission over the defined stream.
    'This is done by converting the element to its corresponding index value in [possibleValues].
    Public Function permuteeToBytes(permutee() As Integer) As Byte()
        Dim permuteeCorrespondingIndices As New List(Of Byte)
        For Each x As Integer In permutee
            permuteeCorrespondingIndices.Add(x)
        Next
        Return permuteeCorrespondingIndices.ToArray
    End Function

    'The inverse operation to permuteeToBytes.
    'Use this when reading data out of the stream.
    Public Function bytesToPermutee(bs() As Byte) As T()
        Dim permuteeCorrespondingIndices As New List(Of T)
        For Each b As Byte In bs
            permuteeCorrespondingIndices.Add(possibleValues(b))
        Next
        Return permuteeCorrespondingIndices.ToArray
    End Function

    'A recursive sub for (O(n!)) permutation. Works by simply repeating the recurive procedure for every element in the permutee.
    Private Sub basicPermutation(ByRef res As List(Of T()), ByRef permutee As Integer(), ByRef n As Integer)
        If n = 1 Then
            addResultToList(res, permutee.ToList)
            Exit Sub
        End If
        For i As Integer = 0 To n - 1
            swap(permutee(i), permutee(n - 1))
            basicPermutation(res, permutee, n - 1)
            swap(permutee(i), permutee(n - 1))
        Next
    End Sub

    'A simple function to generate a random permutation of length [sizeOfPermutation]. Can allow or disallow repeated elements.
    'The seed is specified by the wrapper function getRandomPermutation(seed) method.
    Private Function randomPermutation(ByVal generator As Random) As List(Of T)
        Dim res As New List(Of T)

        If (Not allowDuplicates) And (sizeOfPermutation > possibleValues.Length) Then
            Throw New Exception("ERROR: [sizeOfPermutation] attribute must be lower than magnitude of [possibleValues] if duplicate elements are not allowed. ")
        End If

        For i As Integer = 1 To sizeOfPermutation
            Dim index As Integer = generator.Next(0, possibleValues.Length)
            If allowDuplicates Then
                res.Add(possibleValues(index))
            Else
                Dim newval As Boolean = False
                While Not newval
                    If Not res.Contains(possibleValues(index)) Then
                        newval = True
                        res.Add(possibleValues(index))
                    End If
                    If Not newval Then index = generator.Next(0, possibleValues.Length)
                End While
            End If
        Next
        Return res
    End Function

    '/////////////////////////
    'These methods provide the main interface for meaningful utilisation of the code.
    '/////////////////////////

    'Returns the number of permutations that the permutor will generate if called.
    'Calucates differently depending on whether duplicate elements are allowed.
    'A return value of -1 indicates more than 2^64 results are returned by permuting.
    Public Function getNoOfPermutations() As Long
        Try
            If Not allowDuplicates Then Return (factorial(possibleValues.Count)) / factorial(possibleValues.Count - sizeOfPermutation)
            Return Math.Pow(possibleValues.Count, sizeOfPermutation)
        Catch ex As Exception
        End Try
        Return -1
    End Function

    'Sets up the stream; call this first
    Public Sub initStreamPermutor()
        streamHandler = New StreamHandler(Of T)(Me)
    End Sub

    'Returns true if the stream is still active; use this to iterate through permutations
    Public Function isStreamActive() As Boolean
        If streamHandler Is Nothing Then Return False
        Return streamHandler.streamActive
    End Function

    'Returns an array of the permutation and sets up the stream to send the next permutation
    Public Function getPermutationFromStream() As T()
        If streamHandler Is Nothing Then Return Nothing
        If Not streamHandler.streamActive Then Return Nothing
        Return streamHandler.getPermutation
    End Function

    'Returns an array of the random permutation generated using the given seed. The seed defaults to using the system tick counter if not specified.
    Public Function getRandomPermutation(ByRef generator As Random) As T()
        Return randomPermutation(generator).ToArray
    End Function

    'Generates every permutation and streams it through [stream].
    'The permutor is set up by the [streamHandler] created by initStreamPermutor().
    'You should NOT call this function.
    Public Sub streamPermutor(ByRef stream As System.IO.MemoryStream,
                               ByRef permutationAvle As Threading.Semaphore,
                               ByRef permutationPost As Threading.Semaphore,
                               ByRef permutationLock As Threading.Semaphore)

        validate(False)

        Dim permutee As List(Of Integer) = initPermutingArray()
        stream.Capacity = sizeOfPermutation
        Do
            outputHandler(permutee.ToArray, stream, permutationAvle, permutationPost, permutationLock)
            findNextPermutation(permutee)
        Loop Until permuteeContainsOnlyFinalElement(permutee)
        outputHandler(permutee.ToArray, stream, permutationAvle, permutationPost, permutationLock)

        permutationAvle.WaitOne()
        stream.Close()
        streamHandler = Nothing
    End Sub

    'Generates every permutation and returns it using a list.
    'This may fail if the number of permutations is too high and VB cannot handle the list; in this case, use permuteToStream().
    '   (This occurs when the list reaches a 2GB object size or contains 2^28 references.)
    Public Function permuteToList() As List(Of T())
        validate(True)

        Dim permutee As List(Of Integer) = initPermutingArray()
        Dim res As New List(Of T())
        Do
            If Not allowDuplicates And permutee.Distinct.Count = permutee.Count Then addResultToList(res, permutee)
            If allowDuplicates Then addResultToList(res, permutee)
            findNextPermutation(permutee)
        Loop Until permuteeContainsOnlyFinalElement(permutee)
        If Not allowDuplicates And permutee.Distinct.Count = permutee.Count Then addResultToList(res, permutee)
        If allowDuplicates Then addResultToList(res, permutee)

        Return res
    End Function

    'Faster but specific method of permuting an array of length [possibleValues.Count] without repetition. Works using recursion in basicPermutation().
    'Basic permuting through a stream is not implemented because I'm lazy.
    Public Function basicPermuteToList() As List(Of T())
        validate(True)
        Dim res As New List(Of T())
        basicPermutation(res, possibleValueIndices.ToArray, possibleValueIndices.Count)
        Return res
    End Function
End Class

'/////////////////////////
'This class provides a compact set of methods and attributes to make safely accessing the stream clean and simple.
'/////////////////////////
Public Class StreamHandler(Of T)
    Private stream As System.IO.MemoryStream = New System.IO.MemoryStream()
    Private permutationAvle, permutationPost, permutationLock As Threading.Semaphore
    Private permutor As PermutationLibrary(Of T)

    'Constructor that configures semaphores for safe data transfer.
    'Also initiates the new thread that computes permutations
    Public Sub New(permutor As PermutationLibrary(Of T))
        permutationAvle = New Threading.Semaphore(1, 1)
        permutationPost = New Threading.Semaphore(0, 1)
        permutationLock = New Threading.Semaphore(1, 1)
        Dim permutationThread As New Threading.Thread(New Threading.ThreadStart(Sub() permutor.streamPermutor(stream, permutationAvle, permutationPost, permutationLock)))

        Me.permutor = permutor
        permutationThread.Start()
    End Sub

    'Returns true is the stream is active
    Public Function streamActive() As Boolean
        If stream.CanRead Then Return True
        Return False
    End Function

    'Opens the lock, gets data from the stream, and closes it again for the permutor to post the next permutation.
    'BUG: The returned bytes are of the wrong length and shortening it returns erroneous data.
    '   A fix has been applied but its efficacy is not guaranteed!
    Public Function getPermutation() As T()
        Dim permutationBytes(permutor.getSizeOfPermutation) As Byte

        permutationPost.WaitOne()
        permutationLock.WaitOne()
        stream.Position = 0
        stream.Read(permutationBytes, 0, permutor.getSizeOfPermutation)
        permutationAvle.Release()
        permutationLock.Release()

        Return permutor.bytesToPermutee(permutationBytes.Take(permutationBytes.Length - 1).ToArray)
    End Function
End Class