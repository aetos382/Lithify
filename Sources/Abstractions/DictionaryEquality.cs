using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// 不変辞書の内容による等価性。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ImmutableDictionary{TKey, TValue}"/> の既定の等価性は<em>参照比較</em>である。
/// これを <c>record</c> のメンバーにすると、同じ内容から別々に組み立てた 2 つの
/// <c>record</c> が等しくないと判定される。<see cref="DocumentMetadata"/> と
/// <see cref="MetadataValue.Mapping"/> はどちらも値としての意味を持ち、
/// 「メタデータだけを読む軽量パスと完全パースの結果が一致する」という契約を
/// 等価性で検証するので、内容で比較しなければならない。
/// </para>
/// <para>
/// 辞書の<em>順序</em>は意味を持たないので、比較も列挙順に依存しない。
/// <see cref="ImmutableDictionary{TKey, TValue}"/> の列挙順は内部の木の形に依存し、
/// 同じ内容でも挿入順によって変わりうる。
/// </para>
/// </remarks>
internal static class DictionaryEquality
{
    /// <summary>
    /// 2 つの辞書が同じ内容かどうかを判定する。
    /// </summary>
    /// <typeparam name="TKey">キーの型。</typeparam>
    /// <typeparam name="TValue">値の型。</typeparam>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns>同じ内容の場合は <see langword="true"/>。</returns>
    [Pure]
    public static bool Equals<TKey, TValue>(
        ImmutableDictionary<TKey, TValue> left,
        ImmutableDictionary<TKey, TValue> right)
        where TKey : notnull
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        var comparer = EqualityComparer<TValue>.Default;

        foreach (var entry in left)
        {
            if (!right.TryGetValue(entry.Key, out var value) ||
                !comparer.Equals(entry.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 辞書の内容からハッシュ値を計算する。
    /// </summary>
    /// <typeparam name="TKey">キーの型。</typeparam>
    /// <typeparam name="TValue">値の型。</typeparam>
    /// <param name="dictionary">対象の辞書。</param>
    /// <returns>ハッシュ値。</returns>
    /// <remarks>
    /// 各項目のハッシュを <see cref="int"/> の加算で畳み込む。順序に依存しない演算でなければ、
    /// 列挙順が異なる同内容の辞書が別のハッシュ値を持ち、
    /// <see cref="Equals"/> との整合が崩れる。
    /// <see cref="HashCode"/> は加える順序に依存するので使えない。
    /// </remarks>
    [Pure]
    public static int GetHashCode<TKey, TValue>(
        ImmutableDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        if (dictionary is null)
        {
            return 0;
        }

        var hash = dictionary.Count;

        // 加算は Directory.Build.props の CheckForOverflowUnderflow で例外になるので、
        // ハッシュの畳み込みは unchecked で行う。桁溢れは想定された挙動である。
        unchecked
        {
            foreach (var entry in dictionary)
            {
                hash += HashCode.Combine(entry.Key, entry.Value);
            }
        }

        return hash;
    }
}
