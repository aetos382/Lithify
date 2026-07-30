using System.CommandLine;

namespace Lithify.Hosting;

/// <summary>
/// CLI にサブコマンドを提供する。
/// </summary>
/// <remarks>
/// <para>
/// <c>Lithify.Hosting</c> がサブコマンドの一覧を持たないようにするための拡張点である。
/// <c>serve</c> は <c>Lithify.Serve</c> が <c>AddDevelopmentServer()</c> の中でこの型を
/// 登録することで現れる。<c>Lithify.Serve</c> を参照していないプロジェクトのヘルプには
/// 出ないので、「使えないコマンドが見える」ことがない。
/// </para>
/// <para>
/// 実装は DI に複数登録でき、<see cref="Symbol.Name"/> の辞書順で
/// ルート コマンドに追加される。列挙順に依存しないのは、ヘルプの表示順が
/// DI の登録順で変わると再現しないため。
/// </para>
/// </remarks>
public interface ILithifyCommandProvider
{
    /// <summary>
    /// サブコマンドを構築する。
    /// </summary>
    /// <returns>ルート コマンドに追加するサブコマンド。</returns>
    /// <remarks>
    /// 呼び出しは 1 回のみである。<see cref="Command.SetAction(System.Func{ParseResult, System.Threading.CancellationToken, System.Threading.Tasks.Task{int}})"/>
    /// でアクションを設定して返す。
    /// </remarks>
    Command CreateCommand();
}
