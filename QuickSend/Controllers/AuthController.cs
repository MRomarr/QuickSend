using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using QuickSend.Setting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QuickSend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JwtSettings _jwtSettings;


        //  I’m not following best practices for this controller



        public AuthController(UserManager<IdentityUser> userManager, JwtSettings jwtSettings = null)
        {
            _userManager = userManager;
            _jwtSettings = jwtSettings;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new IdentityUser
            {
                UserName = model.Username,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                return Ok(new { message = "User registered successfully" });
            }

            return BadRequest(result.Errors);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);

            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var token = GenerateJwtTokenAsync(user);
                return Ok(new { token });
            }

            return Unauthorized(new { message = "Invalid username or password" });
        }
        private async Task<string> GenerateJwtTokenAsync(IdentityUser user)
        {
            var userClimas = await _userManager.GetClaimsAsync(user);
            var roles = await _userManager.GetRolesAsync(user);
            var rolesClimas = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
            var climas = new List<Claim>()
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
            };
            climas.AddRange(userClimas);
            climas.AddRange(rolesClimas);

            var issuer = _jwtSettings.Issuer ?? throw new InvalidOperationException("JWT issuer is missing.");
            var audience = _jwtSettings.Audience ?? throw new InvalidOperationException("JWT audience is missing.");
            var key = _jwtSettings.Secret ?? throw new InvalidOperationException("JWT key is missing.");

            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);


            var jwtToken = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: climas,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes),
                signingCredentials: signingCredentials
            );
            
            return new JwtSecurityTokenHandler().WriteToken(jwtToken);
        }


    }
    public class RegisterDto
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class LoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

}
