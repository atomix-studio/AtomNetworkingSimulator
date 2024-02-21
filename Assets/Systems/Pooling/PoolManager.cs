using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance
    {
        get
        {
            return _instance;
        }
    }
    private static PoolManager _instance;

    private static Dictionary<string, List<GameObject>> _go_pool = new Dictionary<string, List<GameObject>>();
    private static Dictionary<string, List<GameObject>> _go_pool_out = new Dictionary<string, List<GameObject>>();

    private int instanceIdGenerator = 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        _instance = this;
        instanceIdGenerator = 0;
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        _go_pool.Clear();
        _go_pool_out.Clear();
    }

    public GameObject SpawnGo(GameObject toSpawnPrefab, Vector3 position, Transform parent = null, object[] spawnData = null)
    {
        GameObject spawned = null;

        // Recherche dans les pools si une instance est disponible ou création
        if (_go_pool.ContainsKey(toSpawnPrefab.name))
        {
            if (_go_pool[toSpawnPrefab.name].Count > 0)
            {
                List<GameObject> pool = _go_pool[toSpawnPrefab.name];
                spawned = pool[0];
                pool.RemoveAt(0);
                //spawned.transform.SetParent(parent == null ? pView.gameObject.transform : parent);
            }

            if (spawned == null)
            {
                spawned = Instantiate(toSpawnPrefab, parent == null ? gameObject.transform : parent);
                spawned.name = toSpawnPrefab.name;
            }
        }
        else
        {
            _go_pool.Add(toSpawnPrefab.name, new List<GameObject>());
            spawned = Instantiate(toSpawnPrefab, parent == null ? gameObject.transform : parent);
            spawned.name = toSpawnPrefab.name;
        }

        // Stockage des objets en cours d'utilisation dans les Unpools
        if (!_go_pool_out.ContainsKey(toSpawnPrefab.name))
        {
            _go_pool_out.Add(toSpawnPrefab.name, new List<GameObject>());
        }
        _go_pool_out[toSpawnPrefab.name].Add(spawned);

        if (parent != null)
        {
            if (spawned.transform.parent != parent)
                spawned.transform.SetParent(parent);

            spawned.transform.localPosition = position + toSpawnPrefab.transform.localPosition; //offset
            //spawned.transform.rotation = toSpawnPrefab.transform.localRotation * spawned.transform.parent.rotation;
            spawned.transform.localRotation = toSpawnPrefab.transform.localRotation;
        }
        else
        {
            // Positionnement de l'objet et activation
            if (position != new Vector3(-1, -1, -1))
            {
                spawned.transform.position = position;
            }
        }

        spawned.gameObject.SetActive(true);

        if (spawnData != null)
        {
            // Initialisation       
            spawned.BroadcastMessage("OnSpawn", spawnData, SendMessageOptions.RequireReceiver);
        }

        return spawned;
    }

    public void DespawnGo(GameObject toDespawn)
    {
        toDespawn.SetActive(false);

        if (_go_pool_out.ContainsKey(toDespawn.name))
        {
            _go_pool_out[toDespawn.name].Remove(toDespawn);
        }

        if (_go_pool.ContainsKey(toDespawn.name))
        {
            if (_go_pool[toDespawn.name].Contains(toDespawn))
            {
                Debug.Log(toDespawn + " is already in the pool.");
            }
            else
                _go_pool[toDespawn.name].Add(toDespawn);
        }
        else
        {
            _go_pool.Add(toDespawn.name, new List<GameObject>());
            _go_pool[toDespawn.name].Add(toDespawn);
        }
    }
}

public class GenericPool<T> where T : UnityEngine.Object
{
    public Queue<T> pool = new Queue<T>();
    public List<T> unpooled = new List<T>();

    public T Spawn(T original)
    {
        T obj = pool.Dequeue();

        if (obj == null)
            obj = MonoBehaviour.Instantiate<T>(original);

        unpooled.Add(obj);

        return obj;
    }

    public void Despawn(T obj)
    {
        pool.Enqueue(obj);
        unpooled.Remove(obj);
    }
}
