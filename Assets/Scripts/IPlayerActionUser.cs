// 職業に依存せず、所持欄の選択・使用状態を表示するための共通契約です。
public interface IPlayerActionUser
{
    ActionInventory Inventory { get; }
    int SelectedSlot { get; }
    BrawlerDefines.ActionUseStatus SelectedActionUseStatus { get; }
}
