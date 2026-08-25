using Microsoft.EntityFrameworkCore;
using MockInterviews.Data.Contexts;
using MockInterviews.Interfaces.IServices;

namespace MockInterviews.Services
{
    public abstract class EntityService<T> : IAccessData<T> where T : class
    {
        protected readonly MockInterviewsDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public EntityService(MockInterviewsDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context), "Database context cannot be null.");
            _dbSet = context.Set<T>();
        }   

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task AddRange(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        public async Task<T> UpdateAsync(T entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                var trackedEntity = _context.ChangeTracker.Entries<T>()
                    .FirstOrDefault(e => e.Entity.Equals(entity));

                if (trackedEntity != null)
                {
                    trackedEntity.CurrentValues.SetValues(entity);
                }
                else
                {
                    var key = _context.Model.FindEntityType(typeof(T))
                                .FindPrimaryKey()
                                .Properties
                                .Select(x => x.Name)
                                .FirstOrDefault();

                    var keyValue = typeof(T).GetProperty(key)?.GetValue(entity);

                    var existingEntity = await _dbSet.FindAsync(keyValue);
                    if (existingEntity != null)
                    {
                        _context.Entry(existingEntity).CurrentValues.SetValues(entity);
                    }
                    else
                    {
                        _dbSet.Attach(entity);
                        _context.Entry(entity).State = EntityState.Modified;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }


        public async Task<bool> DeleteAsync(object id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity == null)
            {
                return false;
            }

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
