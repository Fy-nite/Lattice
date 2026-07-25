# Missing stdlib functions

Current stdlib coverage is minimal (14 methods in 6 classes). Here are the gaps.

---

## Math

No math library at all. Every benchmark currently reimplements loops.

| Priority | Function | Reason |
|---|---|---|
| High | `Math.Sqrt(float32) -> float32` | Needed for any real computation |
| High | `Math.Abs(int32) -> int32` | Needed for `Math.Abs(float32) -> float32` |
| High | `Math.Min(int32, int32) -> int32` | `Math.Min(float32, float32) -> float32` |
| High | `Math.Max(int32, int32) -> int32` | `Math.Max(float32, float32) -> float32` |
| Medium | `Math.Pow(float32, float32) -> float32` | |
| Medium | `Math.Floor(float32) -> int32` | `Math.Ceiling(float32) -> int32` |
| Low | `Math.Sin`, `Cos`, `Tan` | Only if doing graphics/physics |
| Low | `Math.Log`, `Log10`, `Exp` | |
| Low | `Math.Round(float32) -> int32` | |
| Low | `Random.NextInt(int32) -> int32` | `Random.NextFloat() -> float32` |

---

## String

| Priority | Function | Reason |
|---|---|---|
| High | `String.Length(string) -> int32` | Needed for any string processing |
| High | `String.Concat(string, string) -> string` | Needed for string building |
| Medium | `String.Substring(string, int32, int32) -> string` | |
| Medium | `String.IndexOf(string, string) -> int32` | |
| Medium | `String.StartsWith`, `EndsWith` | |
| Medium | `String.Trim()` | |
| Medium | `String.ToUpper`, `ToLower` | |
| Low | `String.Split(string, string) -> string[]` | Would need array support |

---

## Collections / Arrays

| Priority | Function | Reason |
|---|---|---|
| High | `Array.Length(any[]) -> int32` | Needed for array iteration |
| Medium | `Array.Get(any[], int32) -> any` | Already doable via `ldelem`/`stelem` in OIR |
| Medium | `Array.Set(any[], int32, any) -> void` | |
| Low | `List.Create() -> List` | Dynamic collections |
| Low | `List.Add(List, any) -> void` | |
| Low | `List.Get(List, int32) -> any` | |
| Low | `Dict.Create() -> Dict` | |
| Low | `Dict.Set(Dict, any, any) -> void` | |
| Low | `Dict.Get(Dict, any) -> any` | |

---

## File I/O

| Priority | Function | Reason |
|---|---|---|
| High | `File.ReadAllText(string) -> string` | Needed for any file work |
| High | `File.WriteAllText(string, string) -> void` | |
| Medium | `File.Exists(string) -> bool` | |
| Medium | `File.ReadAllLines(string) -> string[]` | Would need array support |
| Low | `Directory.GetFiles`, `GetDirectories` | |
| Low | `File.Copy`, `Move`, `Delete` | |

---

## Conversion / Parsing

| Priority | Function | Reason |
|---|---|---|
| High | `Convert.ToInt32(string) -> int32` | Needed for text-to-number |
| High | `Convert.ToString(int32) -> string` | Needed for number-to-text |
| Medium | `Convert.ToFloat32(string) -> float32` | |
| Medium | `Convert.ToInt32(float32) -> int32` | Truncation |
| Low | `Convert.ToBool(string) -> bool` | |

---

## Time

| Priority | Function | Reason |
|---|---|---|
| Medium | `Time.Now() -> int64` | Timestamps, seed RNG |
| Low | `Time.Format(int64, string) -> string` | Date formatting |

---

## Debug / Environment

| Priority | Function | Reason |
|---|---|---|
| Medium | `Debug.Assert(bool, string) -> void` | Testing |
| Low | `Environment.GetEnv(string) -> string` | |
| Low | `Environment.Exit(int32) -> void` | |
| Low | `GC.Collect() -> void` | Force garbage collection |

---

## Summary of gaps by priority

| Priority | Functions needed |
|---|---|
| **High (9)** | Math.Sqrt, Math.Abs, Math.Min, Math.Max, String.Length, String.Concat, Array.Length, File.ReadAllText, File.WriteAllText, Convert.ToInt32, Convert.ToString |
| **Medium (14)** | Math.Pow, Math.Floor, Math.Ceiling, String.Substring, String.IndexOf, String.StartsWith, String.EndsWith, String.Trim, String.ToUpper, String.ToLower, Array.Get, Array.Set, File.Exists, Time.Now, Convert.ToFloat32, Debug.Assert |
| **Low (~15)** | Math.Sin/Cos/Tan, Math.Log/Exp, Random, String.Split, List, Dict, Directory, File.Copy/Move/Delete, Convert.ToBool, Time.Format, Environment, GC |
