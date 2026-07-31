using System;
using System.Collections.Immutable;
using System.Globalization;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// メタデータの値。
/// </summary>
/// <remarks>
/// <para>
/// YAML フロント マターと AsciiDoc の document attributes の双方を表現できる最小の形。
/// スカラーを文字列として保持するのは、日付や数値の解釈が消費者の関心であって
/// パーサーの関心ではないためである（<c>date</c> の書式は Blog パッケージが決める）。
/// </para>
/// <para>
/// <see cref="Flag"/> を分けているのは AsciiDoc の <c>:toc:</c> / <c>:!toc:</c> が
/// 「値のない真偽」を表すためで、これを空文字列のスカラーに潰すと未設定と区別できなくなる。
/// </para>
/// </remarks>
// 派生型を入れ子にすることで閉じた階層を表現している。外に出すと利用者が任意の派生型を
// 追加できてしまい、網羅的なパターン マッチが成立しなくなる。
#pragma warning disable CA1034 // Nested types should not be visible
public abstract record MetadataValue
{
    private MetadataValue()
    {
    }

    /// <summary>
    /// この値の種類を表す名前を取得する。エラー メッセージに用いる。
    /// </summary>
    public abstract string Kind { get; }

    /// <summary>
    /// 文字列として表現された単一の値。
    /// </summary>
    /// <param name="Text">値の文字列表現。</param>
    public sealed record Scalar(string Text) : MetadataValue
    {
        /// <inheritdoc />
        public override string Kind =>
            nameof(Scalar);
    }

    /// <summary>
    /// 値を伴わない真偽。AsciiDoc の <c>:toc:</c> / <c>:!toc:</c> に対応する。
    /// </summary>
    /// <param name="Value">真偽値。</param>
    public sealed record Flag(bool Value) : MetadataValue
    {
        /// <inheritdoc />
        public override string Kind =>
            nameof(Flag);
    }

    /// <summary>
    /// 順序を持つ値の並び。<c>tags</c> のような複数値に対応する。
    /// </summary>
    /// <param name="Items">要素。</param>
    /// <remarks>
    /// 等価性は要素の内容で決まる（<see cref="ImmutableArray{T}"/> の既定の等価性は
    /// 配列の参照比較なので、<see cref="Equals(Sequence)"/> で上書きしている）。
    /// <c>record</c> が値の意味を持つと読める型で参照比較が残ると、
    /// 同じ内容のメタデータが「違う」と判定される。
    /// </remarks>
    public sealed record Sequence(ImmutableArray<MetadataValue> Items) : MetadataValue
    {
        /// <inheritdoc />
        public override string Kind =>
            nameof(Sequence);

        /// <summary>
        /// 要素の内容が等しいかどうかを判定する。
        /// </summary>
        /// <param name="other">比較対象。</param>
        /// <returns>等しい場合は <see langword="true"/>。</returns>
        public bool Equals(
            Sequence? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is not null &&
                this.Items.AsSpan().SequenceEqual(other.Items.AsSpan());
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in this.Items)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// 入れ子になったキーと値の対応。
    /// </summary>
    /// <param name="Entries">キーと値の対応。</param>
    /// <remarks>
    /// <see cref="Sequence"/> と同じ理由で等価性を内容で決める。
    /// 対応の順序は意味を持たないので、比較も順序に依存しない。
    /// </remarks>
    public sealed record Mapping(ImmutableDictionary<MetadataKey, MetadataValue> Entries) : MetadataValue
    {
        /// <inheritdoc />
        public override string Kind =>
            nameof(Mapping);

        /// <summary>
        /// 対応の内容が等しいかどうかを判定する。
        /// </summary>
        /// <param name="other">比較対象。</param>
        /// <returns>等しい場合は <see langword="true"/>。</returns>
        public bool Equals(
            Mapping? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is not null &&
                DictionaryEquality.Equals(this.Entries, other.Entries);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return DictionaryEquality.GetHashCode(this.Entries);
        }
    }

    /// <summary>
    /// スカラーとしての文字列を取得する。
    /// </summary>
    /// <returns>スカラーの文字列表現。</returns>
    /// <exception cref="InvalidOperationException">この値がスカラーとして読めない。</exception>
    /// <remarks>
    /// <see cref="Flag"/> は <c>True</c> / <c>False</c> の文字列として読める。
    /// 形式によって真偽の表現が異なる（YAML の <c>draft: true</c> と AsciiDoc の <c>:draft:</c>）ため、
    /// 消費者が両方を同じ経路で扱えるようにしている。
    /// </remarks>
    [Pure]
    public string AsScalar()
    {
        return this switch
        {
            Scalar scalar => scalar.Text,
            Flag flag => flag.Value ? bool.TrueString : bool.FalseString,
            _ => throw new InvalidOperationException(
                Messages.FormatMetadataValueKindMismatch(nameof(Scalar), this.Kind))
        };
    }

    /// <summary>
    /// 値の並びとして取得する。
    /// </summary>
    /// <returns>要素の並び。単一の値の場合は、その値のみを含む長さ 1 の並び。</returns>
    /// <remarks>
    /// 単一の値を長さ 1 の並びとして読めるようにしているのは、
    /// <c>tags: foo</c> と <c>tags: [foo]</c> をどちらも受け付けるためである。
    /// </remarks>
    [Pure]
    public ImmutableArray<MetadataValue> AsSequence()
    {
        return this is Sequence sequence ? sequence.Items : [this];
    }

    /// <summary>
    /// スカラーとして読めるかどうかを示す値を取得する。
    /// </summary>
    public bool IsScalar =>
        this is Scalar or Flag;

    /// <summary>
    /// 真偽値として解釈する。
    /// </summary>
    /// <param name="value">解釈された真偽値。</param>
    /// <returns>真偽値として解釈できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="Flag"/> はそのまま、<see cref="Scalar"/> は
    /// <see cref="bool.TryParse(string, out bool)"/> で解釈する。
    /// </remarks>
    [Pure]
    public bool TryGetBoolean(
        out bool value)
    {
        switch (this)
        {
            case Flag flag:
                value = flag.Value;
                return true;

            case Scalar scalar:
                return bool.TryParse(scalar.Text, out value);

            default:
                value = false;
                return false;
        }
    }

    /// <summary>
    /// 日付と時刻として解釈する。
    /// </summary>
    /// <param name="value">解釈された日付と時刻。</param>
    /// <returns>日付と時刻として解釈できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 解釈は <see cref="CultureInfo.InvariantCulture"/> で行い、
    /// タイム ゾーンの指定がない場合は UTC とみなす。
    /// 記事の日付がビルド環境のロケールやタイム ゾーンによって変わってはならない。
    /// </remarks>
    [Pure]
    public bool TryGetDateTimeOffset(
        out DateTimeOffset value)
    {
        if (this is Scalar scalar)
        {
            return DateTimeOffset.TryParse(
                scalar.Text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value);
        }

        value = default;
        return false;
    }
}
#pragma warning restore CA1034
