using Esm;

public interface IDialogController
{
	void SetDisposition(int disposition);
	void DisplayTopic(DialogRecord dialog, int choice = -1);
	void DisplayService(CharacterService service);
	void DisplayResult(string result);
	void DisplayChoice(string description, DialogRecord dialog, int choice);
}