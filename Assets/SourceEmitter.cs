using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public class SourceEmitter : MonoBehaviour {

	public enum BattleState {
		None,
		Running,
	}
	private BattleState state;

	// Use this for initialization
	void Start () {
		state = BattleState.Running;

		// 2つ同じ型のレシーバを作っても、一つの型に対して一個しかレシーバ関数を登録できないので、後勝ちになる = 一件しかレシーバが残らない。
		var s = new WantToReceiveMessage1();
		var t = new WantToReceiveMessage1();

		// 擬似的なレシーブ動作
		var data = new Message1();
		data.param = "a";

		var jsonStr = JsonUtility.ToJson(data);
		var jsonBytes = Encoding.UTF8.GetBytes(jsonStr);

		var data2 = new Message2("fufufu");
		
		var jsonStr2 = JsonUtility.ToJson(data2);
		var jsonBytes2 = Encoding.UTF8.GetBytes(jsonStr2);
		
		for (var i = 0; i < 10; i++) {
			var dataBox = jsonBytes;
			if (i % 2 == 1) {
				dataBox = jsonBytes2;
			}
			Input(dataBox);
		}
	}

	public void Input (byte[] data) {
		switch (state) {
			case BattleState.None: {
				// do nothing.
				break;
			}
			case BattleState.Running: {
				// このイベントを受け取らせたいクラスの受け取りメソッドを着火する
				RootReceiver.Invoke<WantToReceiveMessage1>(data);
				break;
			}
		}
	}
}

/**
	オブジェクト型 -> メッセージ型 -> デシリアライズ関数 という機構を持っている。

	特定のインスタンスが、この型のメッセージだけを受け取りたい、という要望を出す -> 実際にそのメッセージが来た時に受け取ることができる。

	対して、渡す側は、特定のタイミングで、特定のインスタンス型に対して、メッセージをbyte[]のまま渡すことができる。
 */
public class RootReceiver {// この機能を、stateごとにレイヤー化できればそれでいい気がする。
	private static RootReceiver thisObject = new RootReceiver(typeof(BaseMessage));

	// private Dictionary<MessageType, MethodInfo> deserializeMethodMap = new Dictionary<MessageType, MethodInfo>();
	private Dictionary<MessageType, Type> typeMap = new Dictionary<MessageType, Type>();

	/**
		base型を拡張している型を列挙して、kind:MessageTypeとして保持している。
	 */
	private RootReceiver (Type baseType) {
		// 存在する型指定識別子 : それを含む型 のマッピングリスト作成
		var typeMapTargetTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(baseType));
		foreach (var typeMapTargetType in typeMapTargetTypes) {
			var targetInstance = Activator.CreateInstance(typeMapTargetType) as BaseMessage;
			var kind = targetInstance.type;
			
			// record kind-type map.
			typeMap[kind] = typeMapTargetType;
		}
	}

	private static Dictionary<Type, Dictionary<MessageType, Action<byte[]>>> observeDict = new Dictionary<Type, Dictionary<MessageType, Action<byte[]>>>();

	/**
		特定の型に対して、特定のメッセージが来た時に実行するメソッドを登録
	 */
	public static void AddObserver<T> (Action<T> action) where T : BaseMessage, new() {
		var kind = new T().type;
		var actionOwnerType = action.Target.GetType();

		if (!observeDict.ContainsKey(actionOwnerType)) {
			observeDict[actionOwnerType] = new Dictionary<MessageType, Action<byte[]>>();
		}

		/*
			こいつがデシリアライザ実装。
			byte[]を引数にもつデシリアライザが実行される、というアクションを生成
		 */
		Action<byte[]> deserializeAndExecuteAct = baseDataBytes => {
			var baseDataStr = Encoding.UTF8.GetString(baseDataBytes);

			// deserialize dataStr to specific typed instance.
			var receivedData = JsonUtility.FromJson<T>(baseDataStr);

			// Tにキャストしてレシーバの関数を叩く、ということができている。
			action((T)receivedData);
		};

		observeDict[actionOwnerType][kind] = deserializeAndExecuteAct;
	}

	// ここでは、対象となるオブジェクトの型を指定して、その型に特定のメッセージが来たという状況で、メソッドを実行させたい。
	public static void Invoke<T> (byte[] data) {
		if (observeDict.ContainsKey(typeof(T))) {
			
			// こっから先がデシリアライズ実装
			// デシリアライザ型の指定
			var dataStr = Encoding.UTF8.GetString(data);
			var kind = JsonUtility.FromJson<BaseMessage>(dataStr).type;

			var actionList = observeDict[typeof(T)];
			foreach (var action in actionList) {
				// このアクションのターゲットがこのkindでなければ無視
				if (kind != action.Key) continue;

				// 実行
				action.Value(data);
			}
		}
	}
}


/**
	この型は、特定のメッセージだけを受け取りたい。
 */
public class WantToReceiveMessage1 {
	public WantToReceiveMessage1 () {
		RootReceiver.AddObserver<Message1>(ReceiveMessage1);
		RootReceiver.AddObserver<Message2>(ReceiveMessage2);
	}

	public void ReceiveMessage1 (Message1 data) {
		Debug.LogError("ReceiveMessage1 received data:" + data.param);
	}

	public void ReceiveMessage2 (Message2 data) {
		Debug.LogError("ReceiveMessage2 received data:" + data.param2);
	}
}


/**
	enum定義
 */
public enum MessageType {
	Base,
	_1,
	_2
}


/**
	型定義
 */
public class BaseMessage {
	public MessageType type;
	public BaseMessage (MessageType type) {
		this.type = type;
	}
}

public class Message1 : BaseMessage {
	public string param;
	public Message1 () : base (MessageType._1) {

	}
}

public class Message2 : BaseMessage {
	public string param2;

	// パラメータ付きコンストラクタを持つ場合は、デフォルトコンストラクタの定義も必要になってしまう。
	// 何らかの方法でこのデータ型と識別子をマッピングしなければいけない都合がある。例えここでデフォルトコンストラクタを作らないで済む実装が作れたとしても、
	// 識別子:この型、というデータを作る必要がある都合上、この型をnewできる引数ゼロのコンストラクが結局必要になる。
	public Message2 () : base (MessageType._2) {
	}
	public Message2 (string param2) : base (MessageType._2) {
		this.param2 = param2;
	}
}


