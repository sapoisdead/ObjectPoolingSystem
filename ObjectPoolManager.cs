using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

public static class ObjectPoolManager
{
    private static GameObject _poolRootFolder;
    private static GameObject _projectilePoolFolder;

    private static Dictionary<GameObject, ObjectPool<GameObject>> _prefabToPoolMap;
    private static Dictionary<GameObject, GameObject> _instanceToPrefabMap;

    private static readonly bool _dontDestroyOnLoad = false;

    public enum PoolType
    {
        Projectile,
    }

    // -------------------------------
    // LAZY INIT
    // -------------------------------
    private static void EnsureInitialized()
    {
        if (_prefabToPoolMap != null) return; // già pronto

        _prefabToPoolMap = new Dictionary<GameObject, ObjectPool<GameObject>>();
        _instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        SetupEmpties();
    }

    private static void SetupEmpties()
    {
        _poolRootFolder = new GameObject("POOLS_FOLDER");
        _projectilePoolFolder = new GameObject("Projectiles");
        _projectilePoolFolder.transform.SetParent(_poolRootFolder.transform);

        if (_dontDestroyOnLoad) { 
        Object.DontDestroyOnLoad(_poolRootFolder); // il pool rimane attivo tra scene
        }
    }

    // -------------------------------
    // CREAZIONE POOL
    // -------------------------------
    private static void CreatePool(GameObject prefab, Vector3 pos, Quaternion rot, PoolType poolType = PoolType.Projectile)
    {
        EnsureInitialized();

        if (_prefabToPoolMap.ContainsKey(prefab))
            return;

        ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
            createFunc: () => CreateObject(prefab, pos, rot, poolType),
            actionOnGet: OnGetObject,
            actionOnRelease: OnReleaseObject,
            actionOnDestroy: OnDestroyObject
        );

        _prefabToPoolMap[prefab] = pool;
    }

    private static GameObject CreateObject(GameObject prefab, Vector3 pos, Quaternion rot, PoolType poolType = PoolType.Projectile)
    {
        GameObject obj = Object.Instantiate(prefab, pos, rot);
        obj.SetActive(false);
        obj.transform.SetParent(GetPoolFolderForType(poolType).transform);
        return obj;
    }

    // -------------------------------
    // CICLO DI VITA
    // -------------------------------
    private static void OnGetObject(GameObject obj) => obj.SetActive(true);
    private static void OnReleaseObject(GameObject obj) => obj.SetActive(false);

    private static void OnDestroyObject(GameObject obj)
    {
        if (_instanceToPrefabMap.ContainsKey(obj))
            _instanceToPrefabMap.Remove(obj);

        Object.Destroy(obj);
    }

    private static GameObject GetPoolFolderForType(PoolType poolType)
    {
        switch (poolType)
        {
            case PoolType.Projectile: return _projectilePoolFolder;
            default: return _poolRootFolder;
        }
    }

    // -------------------------------
    // SPAWN
    // -------------------------------
    private static T SpawnObject<T>(GameObject objectToSpawn, Vector3 spawnPos, Quaternion spawnRotation, PoolType poolType = PoolType.Projectile) where T : Object
    {
        EnsureInitialized();

        if (!_prefabToPoolMap.ContainsKey(objectToSpawn))
        {
            CreatePool(objectToSpawn, spawnPos, spawnRotation, poolType);
        }

        GameObject obj = _prefabToPoolMap[objectToSpawn].Get();

        if (obj != null)
        {
            if (!_instanceToPrefabMap.ContainsKey(obj))
                _instanceToPrefabMap.Add(obj, objectToSpawn);

            obj.transform.position = spawnPos;
            obj.transform.rotation = spawnRotation;

            if (typeof(T) == typeof(GameObject))
                return obj as T;

            T component = obj.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError($"Object {objectToSpawn.name} does not have component of type {typeof(T)}");
                return null;
            }

            return component;
        }

        return null;
    }

    public static T SpawnObject<T>(T typePrefab, Vector3 spawnPos, Quaternion spawnRotation, PoolType poolType = PoolType.Projectile) where T : Component
    {
        return SpawnObject<T>(typePrefab.gameObject, spawnPos, spawnRotation, poolType);
    }

    public static GameObject SpawnObject(GameObject objectToSpawn, Vector3 spawnPos, Quaternion spawnRotation, PoolType poolType = PoolType.Projectile)
    {
        return SpawnObject<GameObject>(objectToSpawn, spawnPos, spawnRotation, poolType);
    }

    // -------------------------------
    // RETURN
    // -------------------------------
    public static void ReleaseToPool(GameObject obj, PoolType poolType = PoolType.Projectile)
    {
        EnsureInitialized();

        if (_instanceToPrefabMap.TryGetValue(obj, out GameObject prefab))
        {
            var parentObject = GetPoolFolderForType(poolType);
            if (obj.transform.parent != parentObject.transform)
                obj.transform.SetParent(parentObject.transform);

            if (_prefabToPoolMap.TryGetValue(prefab, out var pool))
                pool.Release(obj);
        }
        else
        {
            Debug.LogWarning("Trying to return an object that is not pooled: " + obj.name);
        }
    }
}
