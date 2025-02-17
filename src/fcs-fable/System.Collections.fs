//------------------------------------------------------------------------
// shims for things not yet implemented in Fable
//------------------------------------------------------------------------

namespace System.Collections

module Generic =

    type Queue<'T>() =
        let xs = ResizeArray<'T>()

        member _.Clear () = xs.Clear()

        member _.Enqueue (item: 'T) =
            xs.Add(item)

        member _.Dequeue () =
            let item = xs.Item(0)
            xs.RemoveAt(0)
            item

        interface System.Collections.IEnumerable with
            member _.GetEnumerator(): System.Collections.IEnumerator =
                (xs.GetEnumerator() :> System.Collections.IEnumerator)

        interface System.Collections.Generic.IEnumerable<'T> with
            member _.GetEnumerator(): System.Collections.Generic.IEnumerator<'T> =
                xs.GetEnumerator()

module Immutable =
    open System.Collections.Generic

    // not immutable, just a ResizeArray // TODO: immutable implementation
    type ImmutableArray<'T> =
        static member CreateBuilder() = ResizeArray<'T>()

    type ImmutableHashSet<'T>(values: 'T seq) =
        let xs = HashSet<'T>(values)

        static member Create<'T>(values) = ImmutableHashSet<'T>(values)
        static member Empty = ImmutableHashSet<'T>(Array.empty)

        member _.Add (value: 'T) =
            let copy = HashSet<'T>(xs)
            copy.Add(value) |> ignore
            ImmutableHashSet<'T>(copy)

        member _.Union (values: seq<'T>) =
            let copy = HashSet<'T>(xs)
            copy.UnionWith(values)
            ImmutableHashSet<'T>(copy)

        member _.Overlaps (values: seq<'T>) =
            // xs.Overlaps(values)
            values |> Seq.exists (fun x -> xs.Contains(x))

        interface System.Collections.IEnumerable with
            member _.GetEnumerator(): System.Collections.IEnumerator =
                (xs.GetEnumerator() :> System.Collections.IEnumerator)

        interface IEnumerable<'T> with
            member _.GetEnumerator(): IEnumerator<'T> =
                xs.GetEnumerator()

    type ImmutableDictionary<'Key, 'Value when 'Key: equality>(pairs: KeyValuePair<'Key, 'Value> seq) =
        let xs = Dictionary<'Key, 'Value>()
        do for pair in pairs do xs.Add(pair.Key, pair.Value)

        static member CreateRange(items) = ImmutableDictionary<'Key, 'Value>(items)
        static member Empty = ImmutableDictionary<'Key, 'Value>(Array.empty)

        member _.Item with get (key: 'Key): 'Value = xs[key]
        member _.ContainsKey (key: 'Key) = xs.ContainsKey(key)

        member _.Add (key: 'Key, value: 'Value) =
            let copy = Dictionary<'Key, 'Value>(xs)
            copy.Add(key, value)
            ImmutableDictionary<'Key, 'Value>(copy)

        member _.SetItem (key: 'Key, value: 'Value) =
            let copy = Dictionary<'Key, 'Value>(xs)
            copy[key] <- value
            ImmutableDictionary<'Key, 'Value>(copy)

        member _.TryGetValue (key: 'Key): bool * 'Value =
            match xs.TryGetValue(key) with
            | true, v -> (true, v)
            | false, v -> (false, v)

        interface System.Collections.IEnumerable with
            member _.GetEnumerator(): System.Collections.IEnumerator =
                (xs.GetEnumerator() :> System.Collections.IEnumerator)

        interface IEnumerable<KeyValuePair<'Key, 'Value>> with
            member _.GetEnumerator(): IEnumerator<KeyValuePair<'Key, 'Value>> =
                xs.GetEnumerator()

module Concurrent =
    open System.Collections.Generic

    // not thread safe, just a ResizeArray // TODO: threaded implementation
    type ConcurrentStack<'T>() =
        let xs = ResizeArray<'T>()

        member _.Push (item: 'T) = xs.Add(item)
        member _.PushRange (items: 'T[]) = xs.AddRange(items)
        member _.Clear () = xs.Clear()
        member _.ToArray () = xs.ToArray()

        interface System.Collections.IEnumerable with
            member _.GetEnumerator(): System.Collections.IEnumerator =
                (xs.GetEnumerator() :> System.Collections.IEnumerator)
        interface IEnumerable<'T> with
            member _.GetEnumerator(): IEnumerator<'T> =
                xs.GetEnumerator()

    // not thread safe, just a Dictionary // TODO: threaded implementation
    [<AllowNullLiteral>]
    type ConcurrentDictionary<'Key, 'Value>(comparer: IEqualityComparer<'Key>) =
        inherit Dictionary<'Key, 'Value>(comparer)

        new () =
            ConcurrentDictionary<'Key, 'Value>(EqualityComparer.Default)
        new (_concurrencyLevel: int, _capacity: int) =
            ConcurrentDictionary<'Key, 'Value>()
        new (_concurrencyLevel: int, comparer: IEqualityComparer<'Key>) =
            ConcurrentDictionary<'Key, 'Value>(comparer)
        new (_concurrencyLevel: int, _capacity: int, comparer: IEqualityComparer<'Key>) =
            ConcurrentDictionary<'Key, 'Value>(comparer)

        member x.TryAdd (key: 'Key, value: 'Value): bool =
            if x.ContainsKey(key)
            then false
            else x.Add(key, value); true

        member x.TryRemove (key: 'Key): bool * 'Value =
            match x.TryGetValue(key) with
            | true, v -> (x.Remove(key), v)
            | _ as res -> res

        member x.GetOrAdd (key: 'Key, value: 'Value): 'Value =
            match x.TryGetValue(key) with
            | true, v -> v
            | _ -> let v = value in x.Add(key, v); v

        member x.GetOrAdd (key: 'Key, valueFactory: System.Func<'Key, 'Value>): 'Value =
            match x.TryGetValue(key) with
            | true, v -> v
            | _ -> let v = valueFactory.Invoke(key) in x.Add(key, v); v

        // member x.GetOrAdd<'Arg> (key: 'Key, valueFactory: 'Key * 'Arg -> 'Value, arg: 'Arg): 'Value =
        //     match x.TryGetValue(key) with
        //     | true, v -> v
        //     | _ -> let v = valueFactory(key, arg) in x.Add(key, v); v

        member x.TryUpdate (key: 'Key, value: 'Value, comparisonValue: 'Value): bool =
            match x.TryGetValue(key) with
            | true, v when Unchecked.equals v comparisonValue -> x[key] <- value; true
            | _ -> false

        member x.AddOrUpdate (key: 'Key, value: 'Value, updateFactory: System.Func<'Key, 'Value, 'Value>): 'Value =
            match x.TryGetValue(key) with
            | true, v -> let v = updateFactory.Invoke(key, v) in x[key] <- v; v
            | _ -> let v = value in x.Add(key, v); v

        // member x.AddOrUpdate (key: 'Key, valueFactory: 'Key -> 'Value, updateFactory: 'Key * 'Value -> 'Value): 'Value =
        //     match x.TryGetValue(key) with
        //     | true, v -> let v = updateFactory(key, v) in x[key] <- v; v
        //     | _ -> let v = valueFactory(key) in x.Add(key, v); v

        // member x.AddOrUpdate (key: 'Key, valueFactory: 'Key * 'Arg -> 'Value, updateFactory: 'Key * 'Arg * 'Value -> 'Value, arg: 'Arg): 'Value =
        //     match x.TryGetValue(key) with
        //     | true, v -> let v = updateFactory(key, arg, v) in x[key] <- v; v
        //     | _ -> let v = valueFactory(key, arg) in x.Add(key, v); v