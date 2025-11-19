using System.Collections.Generic;

namespace TNov
{
    public class numstostring
    {
        
        public numstostring(in List<int> nums, out string numsstring)
        {
            string str = "";
            int i = 0;
            string div = "";
            //первый этап, получаем массивы смежных чисел
            while (i < nums.Count)
            {
                if (i > 0)
                {
                    if (nums[i] - nums[i - 1] > 1)
                    {
                        div = ",";
                        str += div + nums[i].ToString();
                    }
                    else
                    {
                        div = "-";
                        str += div + nums[i].ToString();
                    }
                }
                else str += div + nums[i].ToString();
                i++;
            }
            
            //второй этап, убираем лишние числа в массивах чисел
            string result = "";
            string[] parts = str.Split(',');
            int i2 = 0;
            string div2 = "";
            foreach (string part in parts)
            {
                if (i2 > 0) { div2 = ","; }
                int counthyphens = 0;
                foreach (char ch in part)
                {
                    char chr = '-';
                    if (ch == chr) { counthyphens++; }
                }
                switch (counthyphens)
                {
                    case 0:
                        result += div2 + part;
                        i2++;
                        break;
                    case 1:
                        string[] partsofpart = part.Split('-');
                        result += div2 + part.Replace("-", ",");
                        i2++;
                        break;
                    default:
                        string[] partsofpart1 = part.Split('-');
                        result += div2 + partsofpart1[0] + "-" + partsofpart1[partsofpart1.Length - 1];
                        i2++;
                        break;
                }

            }
            result = result.Replace("-", " - "); result = result.Replace(",", ", ");

            numsstring = result;
        }
    }
}
