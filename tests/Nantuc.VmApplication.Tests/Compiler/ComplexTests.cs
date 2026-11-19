using Xunit.Abstractions;

namespace Nantuc.Tests.Compiler;

public class ComplexTests(ITestOutputHelper outPut) : CompileTestBase(outPut)
{
    [Theory]
    [InlineData("""
                int main() {
            int a = 1, b = 2, c = 3;
            int d = a + b * c;
            return d;  // 7
        }
        """,7)]
    [InlineData("""
                int main() {
            int x = 3, y = 12;
            x = y - x;
            return x;  // 9
        }
        """, 9)]
    [InlineData("""
                int main() {
            int arr[4];
            int i = 3;
            int x = 99;
            arr[i] = 42;
            return arr[3];  // 42, не arr[99]
        }
        """,42)]
    [InlineData("""
                int main() {
            int arr[4];
            int a = 1, b = 2, c = 3;
            int i = 0;
            arr[i] += a + (b + c);
            return arr[0];  // 6
        }
        """, 6)]
    [InlineData("""
                int main() {
            int a = 5;
            int b = a;
            int c = b;
            return c;  // 5
        }
        
        """, 5)]
    [InlineData("""
                int main() {
            int a=1,b=2,c=3,d=4,e=5,f=6,g=7,h=8;
            int i=9,j=10,k=11,l=12,m=13,n=14,o=15,p=16;
            int q=17,r=18,s=19,t=20,u=21,v=22,w=23;
            int sum = a+b+c+d+e+f+g+h+i+j+k+l+m+n+o+p+q+r+s+t+u+v+w;
            return sum;  // 276
        }
        """,276)]
    [InlineData("""
                int main() {
            int x = 10;
            x += 5;
            x -= 3;
            x *= 2;
            return x;  // 24
        }
        """,24)]
    public void Run(string s, ulong v) => RunCompile(s,v);
   
}
