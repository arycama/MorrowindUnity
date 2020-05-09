using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]//, ExecuteInEditMode]
public class LoadUIImage : MonoBehaviour
{
	[SerializeField]
	private string path;

	private Sprite sprite;

	private void OnValidate()
	{
		name = path;
	}

	private void OnEnable()
	{
		if (!Application.isPlaying && !string.IsNullOrEmpty(path))
		{
			var texture = BsaFileReader.LoadTexture("textures\\" + path) as Texture2D;

			if(texture == null)
			{
				return;
			}

			var image = GetComponent<Image>();
			sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
			image.sprite = sprite;
		}
	}

	private void OnDisable()
	{
		if(!Application.isPlaying && sprite != null)
		{
			GetComponent<Image>().sprite = null;
			sprite = null;
		}
	}

	private void Start()
    {
		if (!Application.isPlaying)
		{
			return;
		}

		gameObject.name = path;

		var texture = BsaFileReader.LoadTexture("textures\\" + path) as Texture2D;

		var image = GetComponent<Image>();
		var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
		image.sprite = sprite;

		Destroy(this);
    }
}