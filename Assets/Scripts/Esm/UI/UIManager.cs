using UnityEngine;

public class UIManager : Singleton<UIManager>
{
	[SerializeField]
	private DialogView dialogUI;

	[SerializeField]
	private InfoPanel infoPanel;

	[SerializeField]
	private ListPanelUI listPanelUI;

	[SerializeField]
	private CharacterMenuUI characterMenu;

	public static DialogView DialogUI => Instance.dialogUI;
	public static InfoPanel InfoPanel => Instance.infoPanel;
	public static ListPanelUI ListPanelUI => Instance.listPanelUI;
	public static CharacterMenuUI CharacterMenu => Instance.characterMenu;
}