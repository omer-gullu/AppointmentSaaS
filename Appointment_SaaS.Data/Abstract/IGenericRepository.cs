using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Data.Abstract
{
    public interface IGenericRepository<T> where T : class
    {
        Task<List<T>> GetAllAsync();
        IQueryable<T> Where(Expression<Func<T, bool>> expression); // n8n'den gelen veriyi filtrelemek için lazım
        Task<T> GetByIdAsync(int id);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task<int> SaveAsync(); // Değişiklikleri SQL'e işleyen asıl komut
    }
}
