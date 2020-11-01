using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

#if false

[UpdateBefore(typeof(TransformSystemGroup))]
public class WeaponEquipSystem_HandlerUsingEntityManager : SystemBase
{
    bool initialized = false;

    public JobHandle commandQueueDependency;

    public struct Command
    {
        public Entity weaponOwner;
        public Weapon currentWeaponRef;
        public int newWeaponIdx;
        public float4x4 localToParent;
    }
    public NativeQueue<Command>.ParallelWriter commandQueueFrontEnd;
    NativeQueue<Command> commandQueue;

    NativeList<Entity> weaponPrefabs;

    protected override void OnCreate()
    {
        commandQueue = new NativeQueue<Command>(Allocator.Persistent);
        commandQueueFrontEnd = commandQueue.AsParallelWriter();

        weaponPrefabs = new NativeList<Entity>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        weaponPrefabs.Dispose();

        commandQueue.Dispose();
    }

    protected override void OnUpdate()
    {
        // Some initialization must happen on first update for singleton entity query to work
        if (!initialized)
        {
            initialized = true;

            var weaponDataEntity = GetSingletonEntity<WeaponDataEntry>();
            var weaponDatabase = EntityManager.GetBuffer<WeaponDataEntry>(weaponDataEntity);

            weaponPrefabs.Clear();
            for (int i = 0; i < weaponDatabase.Length; ++i)
            {
                weaponPrefabs.Add(weaponDatabase[i].prefab);
            }
        }

        commandQueueDependency.Complete();
        while (commandQueue.TryDequeue(out var command))
        {
            if (command.currentWeaponRef.Value != Entity.Null)
            {
                EntityManager.DestroyEntity(command.currentWeaponRef.Value);
            }

            var newWeapon = EntityManager.Instantiate(weaponPrefabs[command.newWeaponIdx]);

            EntityManager.AddComponentData(newWeapon, new Parent { Value = command.weaponOwner });
            EntityManager.AddComponentData(newWeapon, new LocalToParent { Value = command.localToParent });

            EntityManager.SetComponentData(command.weaponOwner, new Weapon { Value = newWeapon });
        }
    }
}

[UpdateBefore(typeof(WeaponEquipSystem_HandlerUsingEntityManager))]
public class WeaponEquipSystemTest_HandlerUsingEntityManager : SystemBase
{
    int currentWeaponIdx;

    EntityQuery m_WeaponOwner;

    protected override void OnCreate()
    {
        m_WeaponOwner = GetEntityQuery(ComponentType.ReadOnly<Weapon>());
    }

    [BurstCompile]
    struct WeaponEquipJob : IJobChunk
    {
        public int weaponToEquipIdx;

        [ReadOnly]
        public EntityTypeHandle entityTypeHandle;

        [ReadOnly]
        public ComponentTypeHandle<Weapon> weaponRefHandle;

        public NativeQueue<WeaponEquipSystem_HandlerUsingEntityManager.Command>.ParallelWriter commandQueue;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var entityChunk = chunk.GetNativeArray(entityTypeHandle);
            var weaponRefChunk = chunk.GetNativeArray(weaponRefHandle);

            for (var i = 0; i < entityChunk.Length; i++)
            {
                var ownerEntity = entityChunk[i];
                var weaponRef = weaponRefChunk[i];

                commandQueue.Enqueue(new WeaponEquipSystem_HandlerUsingEntityManager.Command
                {
                    weaponOwner = ownerEntity,
                    currentWeaponRef = weaponRef,
                    newWeaponIdx = weaponToEquipIdx,
                    localToParent = float4x4.identity
                });
            }
        }
    }

    protected override void OnUpdate()
    {
        ++currentWeaponIdx;
        currentWeaponIdx = currentWeaponIdx % 4;

        var entityTypeHandle = GetEntityTypeHandle();
        var weaponRefHandle = GetComponentTypeHandle<Weapon>(true);

        var weaponEquipSystem = World.GetExistingSystem<WeaponEquipSystem_HandlerUsingEntityManager>();

        var job = new WeaponEquipJob()
        {
            weaponToEquipIdx = currentWeaponIdx,

            entityTypeHandle = entityTypeHandle,
            weaponRefHandle = weaponRefHandle,

            commandQueue = weaponEquipSystem.commandQueueFrontEnd
        };

        Dependency = job.ScheduleSingle(m_WeaponOwner, Dependency);
        weaponEquipSystem.commandQueueDependency = Dependency;
    }
}

#endif
