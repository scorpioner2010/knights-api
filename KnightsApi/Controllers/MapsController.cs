using Microsoft.AspNetCore.Mvc;
using KnightsApi.Data;

namespace KnightsApi.Controllers
{
    [ApiController]
    [Route("maps")]
    public class MapsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public MapsController(AppDbContext db)
        {
            _db = db;
        }

        // GET /maps
        [HttpGet]
        public IActionResult GetAll()
        {
            var items = _db.Maps
                .Select(m => new MapDto
                {
                    Id = m.Id,
                    Code = m.Code,
                    Name = m.Name,
                    Description = m.Description ?? string.Empty
                })
                .ToList();

            return Ok(items);
        }
    }

    public class MapDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}